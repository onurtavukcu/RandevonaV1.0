using Domain.Entities.BaseEntities;
using MongoDB.Driver;
namespace Data.MongoDbContext;
public class MongoDbContext : IMongoDbContext
{
    private readonly ITenantDatabaseResolver _resolver;
    public MongoDbContext(ITenantDatabaseResolver resolver) => _resolver = resolver;
    public async Task<IMongoCollection<T>> GetCollectionAsync<T>(CancellationToken ct = default) where T : TenantBaseEntity
        => (await _resolver.GetDatabaseAsync(ct)).GetCollection<T>(typeof(T).Name);
}
