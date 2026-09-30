using Domain.Models.Identity.User.Login;
using Domain.Models.Shared.Result;

namespace IdentityService.LoginService
{
    public interface ILoginService
    {
        Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    }
}
