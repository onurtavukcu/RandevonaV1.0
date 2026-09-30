using Domain.Entities.Appointment;
using Domain.Entities.Identity.UserEntity;
using MongoDB.Driver;
namespace Data.MongoDbContext;

public static class MongoIndexes
{
    public static async Task EnsureControlAsync(IMongoDatabase database, CancellationToken ct)
    {
        await Create(database, Builders<Users>.IndexKeys.Ascending(x => x.NormalizedEmail),
            "ux_Users_NormalizedEmail", ct, true);
        await Create(database, Builders<Users>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.IsDeleted),
            "idx_Users_Tenant", ct);
        await Create(database, Builders<Tenants>.IndexKeys.Ascending(x => x.OwnerUserId),
            "idx_Tenants_OwnerUserId", ct);
        // Partial index permits old, unprovisioned records without a database mapping.
        var options = new CreateIndexOptions<Tenants>
        {
            Name = "ux_Tenants_DatabaseName", Unique = true,
            PartialFilterExpression = Builders<Tenants>.Filter.Gt(x => x.DatabaseName, "")
        };
        await database.GetCollection<Tenants>(nameof(Tenants)).Indexes.CreateOneAsync(
            new CreateIndexModel<Tenants>(Builders<Tenants>.IndexKeys.Ascending(x => x.DatabaseName), options),
            cancellationToken: ct);
    }
    public static async Task EnsureTenantAsync(IMongoDatabase database, CancellationToken ct)
    {
        await Create(database, Builders<Organizations>.IndexKeys.Ascending(x => x.TenantId)
            .Ascending(x => x.IsDeleted).Ascending(x => x.IsActive), "idx_Organizations_Tenant_Status", ct);
        await Create(database, Builders<Employees>.IndexKeys.Ascending(x => x.TenantId)
            .Ascending(x => x.OrganizationId).Ascending(x => x.IsDeleted).Ascending(x => x.IsActive),
            "idx_Employees_Tenant_Organization_Status", ct);
    }
    private static Task<string> Create<T>(IMongoDatabase database, IndexKeysDefinition<T> keys,
        string name, CancellationToken ct, bool unique = false)
        => database.GetCollection<T>(typeof(T).Name).Indexes.CreateOneAsync(
            new CreateIndexModel<T>(keys, new CreateIndexOptions { Name = name, Unique = unique }), cancellationToken: ct);
}
