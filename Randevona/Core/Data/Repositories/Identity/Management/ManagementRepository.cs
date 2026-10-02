using Data.MongoDbContext;
using Domain.Entities.Identity.UserEntity;
using Domain.Models.Identity.Management;
using Domain.Models.Identity.User.UserInformation;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace Data.Repositories.Identity.Management;

public sealed class ManagementRepository(IControlMongoDbContext control, IMongoClient client, MongoSettings settings) : IManagementRepository
{
    public async Task<ManagementPage<Tenants>> ListTenantsAsync(ManagementQuery query, CancellationToken ct)
    {
        var filter = Builders<Tenants>.Filter.Where(x => !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Search))
            filter &= Builders<Tenants>.Filter.Regex(x => x.CompanyInfos.CompanyName, new BsonRegularExpression(Regex.Escape(query.Search), "i"));
        var collection = control.GetCollection<Tenants>();
        var total = await collection.CountDocumentsAsync(filter, cancellationToken: ct);
        var items = await collection.Find(filter).SortByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * ManagementPage<Tenants>.PageSize).Limit(ManagementPage<Tenants>.PageSize).ToListAsync(ct);
        return new(items, total, query);
    }

    public async Task<ManagementPage<Users>> ListUsersAsync(ManagementQuery query, CancellationToken ct)
    {
        var filter = Builders<Users>.Filter.Where(x => !x.IsDeleted);
        if (query.Status is not null) filter &= Builders<Users>.Filter.Eq(x => x.UserStatus, query.Status.Value);
        if (query.TenantId is not null) filter &= Builders<Users>.Filter.Eq(x => x.TenantId, query.TenantId);
        if (!string.IsNullOrWhiteSpace(query.Search))
            filter &= Builders<Users>.Filter.Regex(x => x.NormalizedEmail, new BsonRegularExpression(Regex.Escape(query.Search.ToUpperInvariant())));
        var collection = control.GetCollection<Users>();
        var total = await collection.CountDocumentsAsync(filter, cancellationToken: ct);
        var items = await collection.Find(filter).SortByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * ManagementPage<Users>.PageSize).Limit(ManagementPage<Users>.PageSize).ToListAsync(ct);
        return new(items, total, query);
    }

    public Task<Tenants?> GetTenantAsync(string id, CancellationToken ct) => control.GetCollection<Tenants>()
        .Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync(ct)!;
    public Task<Users?> GetUserAsync(string id, CancellationToken ct) => control.GetCollection<Users>()
        .Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync(ct)!;
    public async Task<IReadOnlyList<Tenants>> GetTenantsAsync(IEnumerable<string> ids, CancellationToken ct) =>
        await control.GetCollection<Tenants>().Find(Builders<Tenants>.Filter.In(x => x.Id, ids.Distinct()) &
            Builders<Tenants>.Filter.Eq(x => x.IsDeleted, false)).ToListAsync(ct);
    public async Task<IReadOnlyList<Users>> GetUsersAsync(IEnumerable<string> ids, CancellationToken ct) =>
        await control.GetCollection<Users>().Find(Builders<Users>.Filter.In(x => x.Id, ids.Distinct()) &
            Builders<Users>.Filter.Eq(x => x.IsDeleted, false)).ToListAsync(ct);

    public async Task<bool> HasAccessibleOrganizationAsync(Users user, Tenants tenant, CancellationToken ct)
    {
        await TenantDatabaseNaming.ValidateAsync(tenant, control, settings.TenantDatabasePrefix, ct);
        var filter = Builders<Organizations>.Filter.Where(x => x.TenantId == tenant.Id && x.IsActive && !x.IsDeleted);
        if (!user.HasAllOrganizationAccess)
            filter &= Builders<Organizations>.Filter.In(x => x.Id, user.Memberships.Select(x => x.OrganizationId).Where(x => ObjectId.TryParse(x, out _)));
        return await client.GetDatabase(tenant.DatabaseName).GetCollection<Organizations>(nameof(Organizations)).Find(filter).AnyAsync(ct);
    }

    public async Task<bool> TryReviewAsync(string userId, string tenantId, string actorId, UserStatus status, string? reason, CancellationToken ct)
    {
        if (status is not (UserStatus.Active or UserStatus.Rejected)) throw new ArgumentOutOfRangeException(nameof(status));
        // Decision and audit fields change atomically. An old form cannot overwrite a prior decision.
        var now = DateTime.UtcNow;
        var result = await control.GetCollection<Users>().UpdateOneAsync(x => x.Id == userId && x.TenantId == tenantId &&
            x.SystemRole == SystemUserRoleType.User && x.UserStatus == UserStatus.PendingApproval && x.IsActive && !x.IsDeleted,
            Builders<Users>.Update.Set(x => x.UserStatus, status).Set(x => x.ReviewedByUserId, actorId)
                .Set(x => x.ReviewedAtUtc, now).Set(x => x.RejectionReason, reason).Set(x => x.UpdatedAt, now), cancellationToken: ct);
        return result.MatchedCount == 1;
    }
}
