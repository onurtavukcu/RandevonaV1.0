using Domain.Entities.BaseEntities;
using Domain.Entities.Identity.UserEntity;
using Domain.Entities.Whatsapp;
using MongoDB.Driver;
namespace Data.MongoDbContext;
public interface IControlMongoDbContext
{
    IMongoDatabase Database { get; }
    IMongoCollection<T> GetCollection<T>() where T : BaseEntity;
}
public sealed class ControlMongoDbContext : IControlMongoDbContext
{
    public IMongoDatabase Database { get; }
    public ControlMongoDbContext(IMongoClient client, MongoSettings settings)
    {
        settings.Validate();
        Database = client.GetDatabase(settings.DatabaseName);
    }
    public IMongoCollection<T> GetCollection<T>() where T : BaseEntity
    {
        if (typeof(T) != typeof(Users) && typeof(T) != typeof(Tenants) && typeof(T) != typeof(ProviderNumberDirectory))
            throw new InvalidOperationException("Only central accounts, tenants and number routing records belong to the control database.");
        return Database.GetCollection<T>(typeof(T).Name);
    }
}
