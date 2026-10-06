using Domain.Models.Identity.Management;
using Domain.Models.Shared.Result;
namespace IdentityService.ManagementService;
public interface IManagementService
{
    Task<Result<AdminWorkspaceOptions>> GetWorkspaceAsync(string actorId, string tenantId, CancellationToken ct = default);
    Task<Result<Domain.Models.Shared.WorkContext.TenantWorkContext>> SelectWorkspaceAsync(string actorId, string tenantId, string organizationId, CancellationToken ct = default);
    Task<Result<ManagementPage<ManagedTenant>>> GetTenantsAsync(string actorId, ManagementQuery query, CancellationToken ct = default);
    Task<Result<ManagementPage<ManagedUser>>> GetUsersAsync(string actorId, ManagementQuery query, CancellationToken ct = default);
    Task<Result<ManagedTenantDetails>> GetTenantAsync(string actorId, string id, CancellationToken ct = default);
    Task<Result<ManagedUserDetails>> GetUserAsync(string actorId, string id, CancellationToken ct = default);
    Task<Result<bool>> ReviewAsync(string actorId, string userId, ApplicationDecision decision, string? reason, CancellationToken ct = default);
}
