using Domain.Models.Meta;
using Domain.Models.Shared.Result;

namespace BussinessServices.ProviderService;

public interface IProviderService
{
    Task<Result<ConnectionPage>> GetAsync(CancellationToken ct = default);
    Task<Result<bool>> SaveAsync(SaveConnectionRequest request, CancellationToken ct = default);
    Task<Result<bool>> CheckAccessAsync(VerifyConnectionRequest request, CancellationToken ct = default);
}
