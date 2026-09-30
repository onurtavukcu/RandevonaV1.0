using Domain.Entities.Identity.UserEntity;

namespace Data.Repositories.Identity.Registration;

public interface IRegistrationRepository
{
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default);
    Task<bool> TryCreateAsync(Users user, Tenants tenant, CancellationToken ct = default);
}
