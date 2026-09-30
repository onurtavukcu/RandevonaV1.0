using Data.MongoDbContext;
using Domain.Entities.BaseEntities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System.Linq.Expressions;

namespace Data.Repositories;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    private readonly Func<CancellationToken, Task<IMongoCollection<T>>> _collection;
    public Repository(IControlMongoDbContext control)
        : this(_ => Task.FromResult(control.GetCollection<T>())) { }
    protected Repository(Func<CancellationToken, Task<IMongoCollection<T>>> collection) => _collection = collection;
    protected virtual FilterDefinition<T> Scope => Builders<T>.Filter.Eq(x => x.IsDeleted, false);
    protected FilterDefinition<T> Apply(Expression<Func<T, bool>> predicate) => Scope & Builders<T>.Filter.Where(predicate);
    protected virtual FilterDefinition<T> EntityFilter(T entity) => Apply(x => x.Id == entity.Id);
    protected virtual void PrepareCreate(T entity)
    {
        if (!ObjectId.TryParse(entity.Id, out _)) throw new ArgumentException("Invalid entity Id.");
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = null;
    }

    public virtual async Task<T?> GetByIdAsync(string id, CancellationToken ct = default)
        => await (await _collection(ct)).Find(Apply(x => x.Id == id)).FirstOrDefaultAsync(ct);
    public virtual async Task<T?> FindOneAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await (await _collection(ct)).Find(Apply(predicate)).FirstOrDefaultAsync(ct);
    public virtual async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await (await _collection(ct)).Find(Apply(predicate)).ToListAsync(ct);
    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await CountAsync(predicate, ct) > 0;
    public virtual async Task<long> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
        => await (await _collection(ct)).CountDocumentsAsync(predicate is null ? Scope : Apply(predicate), cancellationToken: ct);
    public virtual async Task<IReadOnlyList<T>> GetAllAsync(int limit = 1000, CancellationToken ct = default)
    {
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
        return await (await _collection(ct)).Find(Scope).Limit(limit).ToListAsync(ct);
    }
    public virtual async Task<(IReadOnlyList<T> Items, long TotalCount)> GetPagedAsync(int page, int pageSize,
        Expression<Func<T, bool>>? predicate = null, Expression<Func<T, object>>? orderBy = null,
        bool ascending = true, CancellationToken ct = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 1000) throw new ArgumentOutOfRangeException(nameof(page));
        var filter = predicate is null ? Scope : Apply(predicate);
        var collection = await _collection(ct);
        var total = await collection.CountDocumentsAsync(filter, cancellationToken: ct);
        var sort = orderBy is null ? Builders<T>.Sort.Ascending(x => x.Id)
            : ascending ? Builders<T>.Sort.Ascending(orderBy) : Builders<T>.Sort.Descending(orderBy);
        var items = await collection.Find(filter).Sort(sort).Skip(checked((page - 1) * pageSize)).Limit(pageSize).ToListAsync(ct);
        return (items, total);
    }
    public virtual async Task<T> CreateAsync(T entity, CancellationToken ct = default)
    {
        PrepareCreate(entity);
        await (await _collection(ct)).InsertOneAsync(entity, cancellationToken: ct);
        return entity;
    }
    public virtual async Task<IReadOnlyList<T>> CreateManyAsync(IEnumerable<T> entities, CancellationToken ct = default)
    {
        var list = entities.ToList();
        foreach (var entity in list) PrepareCreate(entity);
        if (list.Count > 0) await (await _collection(ct)).InsertManyAsync(list, cancellationToken: ct);
        return list;
    }
    public virtual async Task<bool> UpdateAsync(T entity, CancellationToken ct = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        var result = await (await _collection(ct)).UpdateOneAsync(EntityFilter(entity),
            new BsonDocumentUpdateDefinition<T>(new BsonDocument("$set", MutableFields(entity))), cancellationToken: ct);
        return result.MatchedCount > 0;
    }
    public virtual async Task<long> UpdateManyAsync(Expression<Func<T, bool>> filter, UpdateDefinition<T> update, CancellationToken ct = default)
    {
        var collection = await _collection(ct);
        var rendered = update.Render(new RenderArgs<T>(collection.DocumentSerializer, BsonSerializer.SerializerRegistry));
        if (!rendered.IsBsonDocument) throw new InvalidOperationException("Update pipelines are not allowed.");
        foreach (var operation in rendered.AsBsonDocument)
        {
            if (operation.Name == "$rename" || !operation.Name.StartsWith('$') || !operation.Value.IsBsonDocument)
                throw new InvalidOperationException("Unsupported update operator.");
            foreach (var field in operation.Value.AsBsonDocument)
                if (ProtectedFields.Contains(field.Name.Split('.')[0]))
                    throw new InvalidOperationException("Identity and scope fields cannot be changed.");
        }
        var combined = Builders<T>.Update.Combine(update, Builders<T>.Update.Set(x => x.UpdatedAt, DateTime.UtcNow));
        return (await collection.UpdateManyAsync(Apply(filter), combined, cancellationToken: ct)).MatchedCount;
    }
    public virtual async Task UpsertAsync(T entity, CancellationToken ct = default)
    {
        PrepareCreate(entity);
        entity.UpdatedAt = DateTime.UtcNow;
        var document = entity.ToBsonDocument();
        var mutable = MutableFields(entity);
        var insertOnly = new BsonDocument(document.Elements.Where(e => !mutable.Contains(e.Name)));
        await (await _collection(ct)).UpdateOneAsync(EntityFilter(entity),
            new BsonDocumentUpdateDefinition<T>(new BsonDocument { { "$set", mutable }, { "$setOnInsert", insertOnly } }),
            new UpdateOptions { IsUpsert = true }, ct);
    }
    public virtual async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
        => await DeleteManyAsync(x => x.Id == id, ct) > 0;
    public virtual async Task<long> DeleteManyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => (await (await _collection(ct)).UpdateManyAsync(Apply(predicate),
            Builders<T>.Update.Set(x => x.IsDeleted, true).Set(x => x.UpdatedAt, DateTime.UtcNow), cancellationToken: ct)).MatchedCount;

    private static readonly HashSet<string> ProtectedFields = ["_id", "TenantId", "OrganizationId", "CreatedAt", "IsDeleted"];
    private static BsonDocument MutableFields(T entity)
    {
        var document = entity.ToBsonDocument();
        foreach (var field in ProtectedFields) document.Remove(field);
        return document;
    }
}
