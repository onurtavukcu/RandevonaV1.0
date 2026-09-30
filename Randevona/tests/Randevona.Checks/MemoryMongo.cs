using System.Reflection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

// Small in-memory Mongo interface double. Driver filters/updates are rendered to BSON,
// then applied here. This verifies routing and repository policy, not Mongo server semantics.
public sealed class MemoryMongo
{
    public Dictionary<(string Db, string Collection), List<BsonDocument>> Documents { get; } = new();
    public HashSet<(string Db, string Collection, string Name)> Indexes { get; } = new();
    public List<(string Db, string Collection, BsonDocument Filter)> Reads { get; } = new();
    public bool FailPing, FailIndexes, DelayPing;
    public int PingCount;
    public IMongoClient Client { get; }
    public MemoryMongo()
    {
        Client = InterfaceProxy.Create<IMongoClient>((method, args) =>
            method.Name == "GetDatabase" ? Database((string)args![0]!) : throw new NotSupportedException(method.Name));
    }
    public List<BsonDocument> Rows(string db, string collection)
    {
        var key = (db, collection);
        if (!Documents.TryGetValue(key, out var list)) Documents[key] = list = new();
        return list;
    }
    public void Seed<T>(string db, T entity) => Rows(db, typeof(T).Name).Add(entity!.ToBsonDocument());
    public IMongoDatabase Database(string db)
        => InterfaceProxy.Create<IMongoDatabase>((method, args) =>
        {
            if (method.Name == "get_DatabaseNamespace") return new DatabaseNamespace(db);
            if (method.Name == "RunCommandAsync") return Ping(args!.OfType<CancellationToken>().Last());
            if (method.Name == "GetCollection")
                return GetType().GetMethod(nameof(Collection))!.MakeGenericMethod(method.GetGenericArguments()[0])
                    .Invoke(this, new object[] { db, args![0]! });
            throw new NotSupportedException(method.Name);
        });
    private async Task<BsonDocument> Ping(CancellationToken ct)
    {
        PingCount++;
        ct.ThrowIfCancellationRequested();
        if (FailPing) throw new TimeoutException("Simulated ping failure");
        if (DelayPing) await Task.Delay(Timeout.Infinite, ct);
        return new BsonDocument("ok", 1);
    }
    public IMongoCollection<T> Collection<T>(string db, string name)
    {
        var registry = BsonSerializer.SerializerRegistry;
        var serializer = registry.GetSerializer<T>();
        var indexes = InterfaceProxy.Create<IMongoIndexManager<T>>((method, args) =>
        {
            if (method.Name != "CreateOneAsync") throw new NotSupportedException(method.Name);
            if (FailIndexes) return Task.FromException<string>(new InvalidOperationException("Simulated index failure"));
            var model = args!.OfType<CreateIndexModel<T>>().Single();
            Indexes.Add((db, name, model.Options.Name!));
            return Task.FromResult(model.Options.Name!);
        });
        return InterfaceProxy.Create<IMongoCollection<T>>((method, args) =>
        {
            if (method.Name == "get_DocumentSerializer") return serializer;
            if (method.Name == "get_Settings") return new MongoCollectionSettings();
            if (method.Name == "get_CollectionNamespace") return new CollectionNamespace(new DatabaseNamespace(db), name);
            if (method.Name == "get_Indexes") return indexes;
            if (method.Name == "get_Database") return Database(db);
            args ??= [];
            foreach (var token in args.OfType<CancellationToken>()) token.ThrowIfCancellationRequested();
            var rows = Rows(db, name);
            if (method.Name == "InsertOneAsync")
            {
                Insert(rows, args[0]!.ToBsonDocument(typeof(T)));
                return Task.CompletedTask;
            }
            if (method.Name == "InsertManyAsync")
            {
                foreach (var entity in (IEnumerable<T>)args[0]!) Insert(rows, entity!.ToBsonDocument());
                return Task.CompletedTask;
            }
            var filter = args.OfType<FilterDefinition<T>>().FirstOrDefault()?.Render(new RenderArgs<T>(serializer, registry))
                ?? new BsonDocument();
            if (method.Name == "FindAsync")
            {
                Reads.Add((db, name, filter));
                var options = args.OfType<FindOptions<T, T>>().FirstOrDefault();
                IEnumerable<BsonDocument> found = rows.Where(row => Matches(row, filter));
                if (options?.Sort is not null)
                {
                    var sort = options.Sort.Render(new RenderArgs<T>(serializer, registry));
                    IOrderedEnumerable<BsonDocument>? ordered = null;
                    foreach (var field in sort)
                    {
                        Func<BsonDocument, BsonValue> selector = row => Value(row, field.Name);
                        ordered = ordered is null
                            ? field.Value.ToInt32() > 0 ? found.OrderBy(selector) : found.OrderByDescending(selector)
                            : field.Value.ToInt32() > 0 ? ordered.ThenBy(selector) : ordered.ThenByDescending(selector);
                    }
                    found = ordered ?? found;
                }
                if (options?.Skip is int skip) found = found.Skip(skip);
                if (options?.Limit is int limit && limit > 0) found = found.Take(limit);
                var result = found.Select(row => BsonSerializer.Deserialize<T>(row)).ToArray();
                return Task.FromResult<IAsyncCursor<T>>(new MemoryCursor<T>(result));
            }
            if (method.Name == "CountDocumentsAsync")
                return Task.FromResult((long)rows.Count(row => Matches(row, filter)));
            if (method.Name is "UpdateOneAsync" or "UpdateManyAsync" or "FindOneAndUpdateAsync")
            {
                // Match and update under the same lock, like a single-document server write.
                lock (rows)
                {
                var update = args.OfType<UpdateDefinition<T>>().Single().Render(new RenderArgs<T>(serializer, registry)).AsBsonDocument;
                var matched = rows.Where(row => Matches(row, filter)).ToArray();
                if (method.Name != "UpdateManyAsync") matched = matched.Take(1).ToArray();
                foreach (var row in matched) Apply(row, update, false);
                BsonValue? upsertedId = null;
                if (matched.Length == 0 && args.OfType<UpdateOptions>().Any(o => o.IsUpsert))
                {
                    var row = new BsonDocument();
                    Apply(row, update, true);
                    Insert(rows, row);
                    upsertedId = row["_id"];
                }
                if (method.Name == "FindOneAndUpdateAsync")
                    return Task.FromResult(matched.Length == 0 ? default(T) : BsonSerializer.Deserialize<T>(matched[0]));
                return Task.FromResult<UpdateResult>(new UpdateResult.Acknowledged(matched.Length, matched.Length, upsertedId));
                }
            }
            throw new NotSupportedException(method.Name);
        });
    }
    private static void Insert(List<BsonDocument> rows, BsonDocument row)
    {
        if (rows.Any(x => x["_id"] == row["_id"])) throw new InvalidOperationException("Duplicate _id");
        rows.Add(row);
    }
    private static BsonValue Value(BsonDocument row, string field)
    {
        BsonValue current = row;
        foreach (var part in field.Split('.'))
        {
            if (!current.IsBsonDocument || !current.AsBsonDocument.TryGetValue(part, out current!)) return BsonNull.Value;
        }
        return current;
    }
    private static bool Matches(BsonDocument row, BsonDocument filter)
    {
        foreach (var field in filter)
        {
            if (field.Name == "$and") { if (!field.Value.AsBsonArray.All(x => Matches(row, x.AsBsonDocument))) return false; continue; }
            if (field.Name == "$or") { if (!field.Value.AsBsonArray.Any(x => Matches(row, x.AsBsonDocument))) return false; continue; }
            var value = Value(row, field.Name);
            if (!field.Value.IsBsonDocument) { if (value != field.Value) return false; continue; }
            foreach (var operation in field.Value.AsBsonDocument)
            {
                var match = operation.Name switch
                {
                    "$eq" => value == operation.Value,
                    "$ne" => value != operation.Value,
                    "$in" => operation.Value.AsBsonArray.Contains(value),
                    "$lte" => value.CompareTo(operation.Value) <= 0,
                    "$gt" => value.CompareTo(operation.Value) > 0,
                    "$exists" => !value.IsBsonNull == operation.Value.AsBoolean,
                    _ => throw new NotSupportedException(operation.Name)
                };
                if (!match) return false;
            }
        }
        return true;
    }
    private static void Apply(BsonDocument row, BsonDocument update, bool inserting)
    {
        foreach (var operation in update)
        {
            if (operation.Name == "$setOnInsert" && !inserting) continue;
            if (operation.Name is "$set" or "$setOnInsert")
                foreach (var field in operation.Value.AsBsonDocument)
                {
                    var parts = field.Name.Split('.');
                    var target = row;
                    foreach (var part in parts.SkipLast(1))
                    {
                        if (!target.TryGetValue(part, out var child) || !child.IsBsonDocument)
                            target[part] = new BsonDocument();
                        target = target[part].AsBsonDocument;
                    }
                    target[parts[^1]] = field.Value.DeepClone();
                }
            else if (operation.Name == "$unset")
                foreach (var field in operation.Value.AsBsonDocument) row.Remove(field.Name);
            else throw new NotSupportedException(operation.Name);
        }
    }
}
public sealed class MemoryCursor<T>(IReadOnlyList<T> rows) : IAsyncCursor<T>
{
    private bool _read;
    public IEnumerable<T> Current => rows;
    public bool MoveNext(CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); if (_read) return false; _read = true; return rows.Count > 0; }
    public Task<bool> MoveNextAsync(CancellationToken ct = default) => Task.FromResult(MoveNext(ct));
    public void Dispose() { }
}
