using Domain.Entities.Identity.UserEntity;

namespace Data.Repositories.Identity.Registration;

public interface IRegistrationRepository
{
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default);
    Task<bool> DatabaseNameExistsAsync(string databaseName, CancellationToken ct = default);
    Task<Users?> FindUserByEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task ClearSystemAdminTenantAsync(string userId, CancellationToken ct = default);
    Task<bool> TryCreateUserAsync(Users user, CancellationToken ct = default);
    Task<bool> TryCreateAsync(Users user, Tenants tenant, CancellationToken ct = default);
}
