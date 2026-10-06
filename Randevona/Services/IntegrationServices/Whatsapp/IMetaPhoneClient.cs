using Domain.Models.Meta;
using Domain.Models.Shared.Result;

namespace IntegrationServices.Whatsapp;
public interface IMetaPhoneClient
{
    Task<Result<MetaPhoneAccess>> CheckAccessAsync(string wabaId, string phoneNumberId, string accessToken, CancellationToken ct = default);
}

