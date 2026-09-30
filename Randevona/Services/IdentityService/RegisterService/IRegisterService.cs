using Domain.Models.Identity.User.Register;
using Domain.Models.Shared.Result;

namespace IdentityService.RegisterService;

public interface IRegisterService
{
    Task<Result<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
}
