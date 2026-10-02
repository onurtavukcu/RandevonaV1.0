using Domain.Entities.Identity.UserEntity;
using Domain.Models.Identity.Management;
using Domain.Models.Identity.User.UserInformation;

namespace Data.Repositories.Identity.Management;

public interface IManagementRepository
{
    Task<ManagementPage<Tenants>> ListTenantsAsync(ManagementQuery query, CancellationToken ct);
    Task<ManagementPage<Users>> ListUsersAsync(ManagementQuery query, CancellationToken ct);
    Task<Tenants?> GetTenantAsync(string id, CancellationToken ct);
    Task<Users?> GetUserAsync(string id, CancellationToken ct);
    Task<IReadOnlyList<Tenants>> GetTenantsAsync(IEnumerable<string> ids, CancellationToken ct);
    Task<IReadOnlyList<Users>> GetUsersAsync(IEnumerable<string> ids, CancellationToken ct);
    Task<bool> HasAccessibleOrganizationAsync(Users user, Tenants tenant, CancellationToken ct);
    Task<bool> TryReviewAsync(string userId, string tenantId, string actorId, UserStatus status, string? reason, CancellationToken ct);
}
