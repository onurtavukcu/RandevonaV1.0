using Domain.Entities.BaseEntities;
using MongoDB.Driver;
namespace Data.MongoDbContext;
public interface IMongoDbContext
{
    Task<IMongoCollection<T>> GetCollectionAsync<T>(CancellationToken ct = default) where T : TenantBaseEntity;
}
