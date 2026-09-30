using Domain.Entities.Identity.UserEntity;
using Domain.Models.Identity.Tenant;
using Domain.Models.Identity.User.UserInformation;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Data.MongoDbContext;

// Called by the trusted registration/admin service after saving the central account and tenant.
// Never expose this directly as an anonymous endpoint.
public sealed class TenantProvisioningService
{
    private readonly IControlMongoDbContext _control;
    private readonly IMongoClient _client;
    private readonly MongoSettings _settings;
    public TenantProvisioningService(IControlMongoDbContext control, IMongoClient client, MongoSettings settings)
        => (_control, _client, _settings) = (control, client, settings);

    public static void PrepareNewTenant(Tenants tenant, string initialOrganizationName)
    {
        if (string.IsNullOrWhiteSpace(initialOrganizationName))
            throw new ArgumentException("Initial organization name is required.");
        if (!string.IsNullOrEmpty(tenant.DatabaseName) || !string.IsNullOrEmpty(tenant.DefaultOrganizationId))
            throw new InvalidOperationException("Existing tenant mapping cannot be reinitialized.");
        tenant.DatabaseName = TenantDatabaseNaming.ForTenant(tenant.Id);
        tenant.DefaultOrganizationId = ObjectId.GenerateNewId().ToString();
        tenant.InitialOrganizationName = initialOrganizationName.Trim();
        tenant.ProvisioningStatus = TenantProvisioningStatus.Pending;
    }

    public async Task ProvisionAsync(string tenantId, CancellationToken ct = default)
    {
        _ = TenantDatabaseNaming.ForTenant(tenantId);
        _settings.Validate();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_settings.StartupTimeoutSeconds));
        var tenants = _control.GetCollection<Tenants>();
        var existing = await tenants.Find(x => x.Id == tenantId && x.IsActive && !x.IsDeleted)
            .FirstOrDefaultAsync(timeout.Token) ?? throw new InvalidOperationException("Tenant not found.");
        TenantDatabaseNaming.Validate(existing, _control.Database.DatabaseNamespace.DatabaseName);
        if (existing.ProvisioningStatus == TenantProvisioningStatus.Active) return;
        if (!ObjectId.TryParse(existing.DefaultOrganizationId, out _) || string.IsNullOrWhiteSpace(existing.InitialOrganizationName))
            throw new InvalidOperationException("Tenant provisioning information is incomplete.");

        var leaseId = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var eligible = Builders<Tenants>.Filter.Where(x => x.Id == tenantId && x.IsActive && !x.IsDeleted &&
            (x.ProvisioningStatus == TenantProvisioningStatus.Pending || x.ProvisioningStatus == TenantProvisioningStatus.Failed ||
            (x.ProvisioningStatus == TenantProvisioningStatus.Provisioning && x.ProvisioningLeaseUntilUtc <= now)));
        var acquired = await tenants.FindOneAndUpdateAsync(eligible,
            Builders<Tenants>.Update.Set(x => x.ProvisioningStatus, TenantProvisioningStatus.Provisioning)
                .Set(x => x.ProvisioningLeaseId, leaseId)
                .Set(x => x.ProvisioningLeaseUntilUtc, now.AddSeconds(_settings.StartupTimeoutSeconds + 30))
                .Set(x => x.UpdatedAt, now),
            new FindOneAndUpdateOptions<Tenants> { ReturnDocument = ReturnDocument.After }, timeout.Token);
        if (acquired is null)
            throw new InvalidOperationException("Tenant is already being provisioned or has changed status.");

        var ownedLease = Builders<Tenants>.Filter.Where(x => x.Id == tenantId &&
            x.ProvisioningLeaseId == leaseId && x.ProvisioningStatus == TenantProvisioningStatus.Provisioning);
        try
        {
            TenantDatabaseNaming.Validate(acquired, _control.Database.DatabaseNamespace.DatabaseName);
            var database = _client.GetDatabase(acquired.DatabaseName);
            await MongoIndexes.EnsureTenantAsync(database, timeout.Token);
            var branch = new Organizations
            {
                Id = acquired.DefaultOrganizationId, TenantId = acquired.Id,
                OrganizationInfos = new OrganizationInfos { OrganizationName = acquired.InitialOrganizationName }
            };
            await database.GetCollection<Organizations>(nameof(Organizations)).UpdateOneAsync(
                x => x.Id == branch.Id && x.TenantId == tenantId,
                new BsonDocumentUpdateDefinition<Organizations>(new BsonDocument("$setOnInsert", branch.ToBsonDocument())),
                new UpdateOptions { IsUpsert = true }, timeout.Token);
            var result = await tenants.UpdateOneAsync(ownedLease,
                Builders<Tenants>.Update.Set(x => x.ProvisioningStatus, TenantProvisioningStatus.Active)
                    .Unset(x => x.ProvisioningLeaseId).Unset(x => x.ProvisioningLeaseUntilUtc)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow), cancellationToken: timeout.Token);
            if (result.MatchedCount != 1) throw new InvalidOperationException("Tenant provisioning lease was lost.");
        }
        catch
        {
            // A short independent token permits recording failure even when the request was cancelled.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try
            {
                await tenants.UpdateOneAsync(ownedLease,
                    Builders<Tenants>.Update.Set(x => x.ProvisioningStatus, TenantProvisioningStatus.Failed)
                        .Unset(x => x.ProvisioningLeaseId).Unset(x => x.ProvisioningLeaseUntilUtc),
                    cancellationToken: cleanup.Token);
            }
            catch { /* If unavailable, the stored lease expires and permits a retry. */ }
            throw;
        }
    }
}
