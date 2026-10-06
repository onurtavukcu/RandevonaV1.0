using CommonServices.WorkContext.ContextAccessor;
using Data.MongoDbContext;
using Data.Repositories.Identity.Management;
using Domain.Entities.Identity.UserEntity;
using Domain.Models.Identity.Management;
using Domain.Models.Identity.Tenant;
using Domain.Models.Identity.User.UserInformation;
using Domain.Models.Shared.Result;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace IdentityService.ManagementService;

public sealed class ManagementService(IManagementRepository repository, TenantProvisioningService provisioning,
    ITenantWorkContextResolver resolver, ILogger<ManagementService> logger) : IManagementService
{
    public Task<Result<AdminWorkspaceOptions>> GetWorkspaceAsync(string actorId, string tenantId, CancellationToken ct = default) =>
        ExecuteAsync<AdminWorkspaceOptions>(actorId, async () =>
        {
            try { return await resolver.GetAdminWorkspaceAsync(actorId, tenantId, ct); }
            catch (UnauthorizedAccessException) { return WorkspaceUnavailable(); }
        }, ct);

    public Task<Result<Domain.Models.Shared.WorkContext.TenantWorkContext>> SelectWorkspaceAsync(string actorId, string tenantId, string organizationId, CancellationToken ct = default) =>
        ExecuteAsync<Domain.Models.Shared.WorkContext.TenantWorkContext>(actorId, async () =>
        {
            try { return await resolver.ResolveAdminAsync(actorId, tenantId, organizationId, ct); }
            catch (UnauthorizedAccessException) { return WorkspaceUnavailable(); }
        }, ct);

    private static Error WorkspaceUnavailable() => new("Management.WorkspaceUnavailable",
        "Select an approved, active business and an active branch belonging to it.", ErrorType.Validation);

    public Task<Result<ManagementPage<ManagedTenant>>> GetTenantsAsync(string actorId, ManagementQuery query, CancellationToken ct = default) =>
        ExecuteAsync<ManagementPage<ManagedTenant>>(actorId, async () =>
        {
            if (!ValidQuery(query)) return InvalidQuery();
            var page = await repository.ListTenantsAsync(query, ct);
            var owners = (await repository.GetUsersAsync(page.Items.Select(x => x.OwnerUserId).Where(ValidId), ct)).ToDictionary(x => x.Id);
            return new ManagementPage<ManagedTenant>(page.Items.Select(x => Tenant(x, owners.GetValueOrDefault(x.OwnerUserId))).ToArray(), page.TotalCount, query);
        }, ct);

    public Task<Result<ManagementPage<ManagedUser>>> GetUsersAsync(string actorId, ManagementQuery query, CancellationToken ct = default) =>
        ExecuteAsync<ManagementPage<ManagedUser>>(actorId, async () =>
        {
            if (!ValidQuery(query)) return InvalidQuery();
            return await UserPageAsync(query, ct);
        }, ct);

    public Task<Result<ManagedTenantDetails>> GetTenantAsync(string actorId, string id, CancellationToken ct = default) =>
        ExecuteAsync<ManagedTenantDetails>(actorId, async () =>
        {
            if (!ValidId(id)) return Missing();
            var tenant = await repository.GetTenantAsync(id, ct);
            if (tenant is null) return Missing();
            var owner = ValidId(tenant.OwnerUserId) ? await repository.GetUserAsync(tenant.OwnerUserId, ct) : null;
            return new ManagedTenantDetails(Tenant(tenant, owner), await UserPageAsync(new() { TenantId = id }, ct));
        }, ct);

    public Task<Result<ManagedUserDetails>> GetUserAsync(string actorId, string id, CancellationToken ct = default) =>
        ExecuteAsync<ManagedUserDetails>(actorId, async () =>
        {
            if (!ValidId(id)) return Missing();
            var user = await repository.GetUserAsync(id, ct);
            if (user is null) return Missing();
            var tenant = ValidId(user.TenantId) ? await repository.GetTenantAsync(user.TenantId!, ct) : null;
            var reviewer = ValidId(user.ReviewedByUserId) ? await repository.GetUserAsync(user.ReviewedByUserId!, ct) : null;
            return new ManagedUserDetails(User(user, tenant?.CompanyInfos.CompanyName), tenant is null ? null : Tenant(tenant, null),
                reviewer is null ? null : User(reviewer), user.HasAllOrganizationAccess, user.Memberships.Count);
        }, ct);

    public Task<Result<bool>> ReviewAsync(string actorId, string userId, ApplicationDecision decision, string? reason, CancellationToken ct = default) =>
        ExecuteAsync<bool>(actorId, async () =>
        {
            if (!ValidId(userId)) return Missing();
            if (!Enum.IsDefined(decision)) return new Error("Management.InvalidDecision", "Choose approve or reject.", ErrorType.Validation);
            reason = reason?.Trim();
            if (decision == ApplicationDecision.Reject && (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000))
                return new Error("Management.ReasonRequired", "Enter a rejection reason between 1 and 1,000 characters.", ErrorType.Validation);
            var user = await repository.GetUserAsync(userId, ct);
            if (user is null) return Missing();
            if (user.Id == actorId || user.SystemRole != SystemUserRoleType.User || user.UserStatus != UserStatus.PendingApproval || !user.IsActive)
                return Conflict();
            if (!ValidId(user.TenantId)) return Unavailable();
            var tenant = await repository.GetTenantAsync(user.TenantId!, ct);
            if (tenant is null) return Unavailable();
            if (decision == ApplicationDecision.Approve)
            {
                if (!tenant.IsActive) return Unavailable();
                if (tenant.OwnerUserId != user.Id)
                {
                    var owner = ValidId(tenant.OwnerUserId) ? await repository.GetUserAsync(tenant.OwnerUserId, ct) : null;
                    if (owner is null || owner.TenantId != tenant.Id || !owner.IsActive || owner.UserStatus != UserStatus.Active)
                        return new Error("Management.OwnerNotApproved", "Approve the business owner's application first.", ErrorType.Validation);
                }
                try
                {
                    await provisioning.ProvisionAsync(tenant.Id, ct);
                    tenant = await repository.GetTenantAsync(tenant.Id, ct);
                    if (tenant is null || !tenant.IsActive || tenant.ProvisioningStatus != TenantProvisioningStatus.Active ||
                        !await repository.HasAccessibleOrganizationAsync(user, tenant, ct)) return Unavailable();
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    logger.LogWarning("Management tenant preparation failed ({ErrorType}).", ex.GetType().Name);
                    return new Error("Management.ProvisioningFailed", "Business setup could not be completed. The application is still pending; try approving again after resolving the setup issue.", ErrorType.Failure);
                }
            }
            // Provisioning may take time; check the operator again before changing the account.
            await resolver.ValidatePlatformAdminAsync(actorId, ct);
            if (!await repository.TryReviewAsync(user.Id, user.TenantId!, actorId,
                decision == ApplicationDecision.Approve ? UserStatus.Active : UserStatus.Rejected,
                decision == ApplicationDecision.Reject ? reason : null, ct)) return Conflict();
            return true;
        }, ct);

    private async Task<ManagementPage<ManagedUser>> UserPageAsync(ManagementQuery query, CancellationToken ct)
    {
        var page = await repository.ListUsersAsync(query, ct);
        var tenants = (await repository.GetTenantsAsync(page.Items.Select(x => x.TenantId).Where(ValidId).Select(x => x!), ct)).ToDictionary(x => x.Id);
        return new(page.Items.Select(x => User(x, x.TenantId is null ? null : tenants.GetValueOrDefault(x.TenantId)?.CompanyInfos.CompanyName)).ToArray(), page.TotalCount, query);
    }

    private async Task<Result<T>> ExecuteAsync<T>(string actorId, Func<Task<Result<T>>> action, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            await resolver.ValidatePlatformAdminAsync(actorId, ct);
            return await action();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (UnauthorizedAccessException) { return new Error("Management.Forbidden", "Platform administrator access is required.", ErrorType.Validation); }
        catch (Exception ex)
        {
            logger.LogError("Management operation failed ({ErrorType}).", ex.GetType().Name);
            return new Error("Management.Failed", "The operation could not be completed. Refresh the page to check the latest state before trying again.", ErrorType.Failure);
        }
    }
    private static bool ValidId(string? value) => ObjectId.TryParse(value, out _);
    private static bool ValidQuery(ManagementQuery query)
    {
        query.Search = query.Search?.Trim();
        query.TenantId = string.IsNullOrWhiteSpace(query.TenantId) ? null : query.TenantId;
        return query.Page is >= 1 and <= 10000 && (query.Search?.Length ?? 0) <= 200 &&
            (query.Status is null || Enum.IsDefined(query.Status.Value)) && (query.TenantId is null || ValidId(query.TenantId));
    }
    private static ManagedUser User(Users user, string? tenantName = null) => new(user.Id, $"{user.FirstName} {user.LastName}".Trim(),
        user.Email, user.TenantId, tenantName, user.SystemRole, user.UserStatus, user.IsActive, user.CreatedAt,
        user.ReviewedByUserId, user.ReviewedAtUtc, user.RejectionReason);
    private static ManagedTenant Tenant(Tenants tenant, Users? owner) => new(tenant.Id, tenant.CompanyInfos.CompanyName,
        tenant.DatabaseName, tenant.OwnerUserId, tenant.InitialOrganizationName, tenant.ProvisioningStatus, tenant.IsActive,
        tenant.CreatedAt, owner is null ? null : User(owner, tenant.CompanyInfos.CompanyName));
    private static Error InvalidQuery() => new("Management.InvalidQuery", "Check the search, status and page filters.", ErrorType.Validation);
    private static Error Missing() => new("Management.NotFound", "The requested account or business was not found.", ErrorType.NotFound);
    private static Error Conflict() => new("Management.ReviewConflict", "This application is no longer pending or cannot be reviewed. Refresh to see its current status.", ErrorType.Conflict);
    private static Error Unavailable() => new("Management.TenantUnavailable", "An active business and an accessible branch are required before approval.", ErrorType.Validation);
}
