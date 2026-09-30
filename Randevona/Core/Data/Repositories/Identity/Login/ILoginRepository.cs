using Domain.Entities.Identity.UserEntity;

namespace Data.Repositories.Identity.Login
{
    public interface ILoginRepository
    {
        Task<Users?> GetUserByEmailAsync(string normalizedEmail, CancellationToken ct = default);
    }
}
