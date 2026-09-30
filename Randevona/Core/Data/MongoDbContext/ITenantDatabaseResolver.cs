using CommonServices.WorkContext.ContextAccessor;
using Domain.Entities.Identity.UserEntity;
using Domain.Models.Identity.Tenant;
using MongoDB.Bson;
using MongoDB.Driver;
namespace Data.MongoDbContext;

public interface ITenantDatabaseResolver
{
    Task<IMongoDatabase> GetDatabaseAsync(CancellationToken ct = default);
}
public static class TenantDatabaseNaming
{
    public static string ForTenant(string tenantId)
    {
        if (!ObjectId.TryParse(tenantId, out var id))
            throw new InvalidOperationException("A valid tenant identifier is required.");
        return "randevona_t_" + id;
    }
    public static void Validate(Tenants tenant, string controlDatabase)
    {
        if (tenant.DatabaseName != ForTenant(tenant.Id) || tenant.DatabaseName == controlDatabase)
            throw new InvalidOperationException("Tenant database mapping is invalid.");
    }
}
public sealed class TenantDatabaseResolver : ITenantDatabaseResolver
{
    private readonly IControlMongoDbContext _control;
    private readonly IMongoClient _client;
    private readonly ITenantContextAccessor _accessor;
    public TenantDatabaseResolver(IControlMongoDbContext control, IMongoClient client, ITenantContextAccessor accessor)
        => (_control, _client, _accessor) = (control, client, accessor);

    public async Task<IMongoDatabase> GetDatabaseAsync(CancellationToken ct = default)
    {
        var id = _accessor.CurrentWorkContext.TenantId;
        _ = TenantDatabaseNaming.ForTenant(id);
        var tenant = await _control.GetCollection<Tenants>()
            .Find(x => x.Id == id && x.IsActive && !x.IsDeleted &&
                x.ProvisioningStatus == TenantProvisioningStatus.Active).FirstOrDefaultAsync(ct)
            ?? throw new UnauthorizedAccessException("Tenant is unavailable.");
        TenantDatabaseNaming.Validate(tenant, _control.Database.DatabaseNamespace.DatabaseName);
        return _client.GetDatabase(tenant.DatabaseName);
    }
}
