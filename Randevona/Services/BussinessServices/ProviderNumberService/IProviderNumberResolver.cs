using Domain.Entities.Whatsapp;
using Domain.Models.Meta;
using Domain.Models.Shared.Result;

namespace BussinessServices.ProviderNumberService;

// Internal service data, never an MVC/API response: Number contains a decrypted access token.
public sealed record ProviderNumberMatch(ProviderData Provider, WhatsAppProviderData Number);
public interface IProviderNumberResolver
{
    Task<Result<ProviderNumberMatch>> ResolveAsync(string tenantId, string organizationId, string? phoneNumberId,
        CancellationToken ct = default);
}
