using CommonServices.WorkContext.ContextAccessor;
using Domain.Entities.Identity.UserEntity;
using Domain.Models.Identity.Tenant;
using Domain.Models.Identity.User.UserInformation;
using Domain.Models.Shared.WorkContext;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Data.MongoDbContext;
public sealed class TenantWorkContextResolver : ITenantWorkContextResolver
{
    private readonly IControlMongoDbContext _control;
    private readonly IMongoClient _client;
    private readonly MongoSettings _settings;
    public TenantWorkContextResolver(IControlMongoDbContext control, IMongoClient client, MongoSettings settings)
        => (_control, _client, _settings) = (control, client, settings);

    public async Task<TenantWorkContext> ResolveAsync(string userId, string tenantId, string? selectedOrganizationId, CancellationToken ct)
    {
        if (!ObjectId.TryParse(userId, out _) || !ObjectId.TryParse(tenantId, out _))
            throw new UnauthorizedAccessException("Invalid account context.");
        var user = await _control.GetCollection<Users>().Find(x => x.Id == userId &&
            x.TenantId == tenantId && x.IsActive && !x.IsDeleted && x.UserStatus == UserStatus.Active)
            .FirstOrDefaultAsync(ct) ?? throw new UnauthorizedAccessException("Account is unavailable.");
        var tenant = await _control.GetCollection<Tenants>().Find(x => x.Id == tenantId &&
            x.IsActive && !x.IsDeleted && x.ProvisioningStatus == TenantProvisioningStatus.Active)
            .FirstOrDefaultAsync(ct) ?? throw new UnauthorizedAccessException("Tenant is unavailable.");
        await TenantDatabaseNaming.ValidateAsync(tenant, _control, _settings.TenantDatabasePrefix, ct);

        var branches = _client.GetDatabase(tenant.DatabaseName).GetCollection<Organizations>(nameof(Organizations));
        var filter = Builders<Organizations>.Filter.Where(x => x.TenantId == tenantId && x.IsActive && !x.IsDeleted);
        if (!user.HasAllOrganizationAccess)
        {
            var ids = user.Memberships.Select(m => m.OrganizationId).Where(id => ObjectId.TryParse(id, out _)).Distinct().ToArray();
            filter &= Builders<Organizations>.Filter.In(x => x.Id, ids);
        }
        var accessible = await branches.Find(filter).SortBy(x => x.Id).ToListAsync(ct);
        var allowedIds = accessible.Select(x => x.Id).ToArray();
        if (!string.IsNullOrWhiteSpace(selectedOrganizationId) && !allowedIds.Contains(selectedOrganizationId))
            throw new UnauthorizedAccessException("Organization access denied.");
        var selected = !string.IsNullOrWhiteSpace(selectedOrganizationId) ? selectedOrganizationId
            : allowedIds.Contains(tenant.DefaultOrganizationId) ? tenant.DefaultOrganizationId
            : allowedIds.FirstOrDefault() ?? string.Empty;
        return new TenantWorkContext(tenantId, selected, userId, user.SystemRole, user.HasAllOrganizationAccess, Array.AsReadOnly(allowedIds));
    }
}
