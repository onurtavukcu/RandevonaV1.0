using Domain.Entities.Whatsapp;

namespace Data.Repositories.Whatsapp;

public interface IProviderDataRepository
{
    Task<ProviderData?> GetAsync(CancellationToken ct = default);
    Task<bool> TrySaveAsync(ProviderData provider, long expectedVersion, ProviderNumberDirectory? newNumber, CancellationToken ct = default);
}

