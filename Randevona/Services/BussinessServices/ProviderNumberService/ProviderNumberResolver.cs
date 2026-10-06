using CommonServices.WorkContext.ContextAccessor;
using Data.Repositories.Whatsapp;
using Domain.Models.Identity.User.UserInformation;
using Domain.Models.Shared.Result;

namespace BussinessServices.ProviderNumberService;

public sealed class ProviderNumberResolver(IProviderDataRepository repository, ITenantContextAccessor accessor,
    ITenantWorkContextResolver contexts) : IProviderNumberResolver
{
    public async Task<Result<ProviderNumberMatch>> ResolveAsync(string tenantId, string organizationId, string? phoneNumberId, CancellationToken ct = default)
    {
        var context = accessor.CurrentWorkContext;
        if (tenantId != context.TenantId || organizationId != context.OrganizationId)
            return new Error("Provider.ScopeMismatch", "The number must belong to the current business and branch.", ErrorType.Validation);
        if (context.SystemUserRoleType == SystemUserRoleType.SuperAdmin)
            await contexts.ResolveAdminAsync(context.UserId, tenantId, organizationId, ct);
        else await contexts.ResolveAsync(context.UserId, tenantId, organizationId, ct);
        var provider = await repository.GetAsync(ct);
        if (provider is null || provider.Numbers.Count == 0)
            return new Error("Provider.NotFound", "No WhatsApp number is configured for this branch.", ErrorType.NotFound);
        if (string.IsNullOrWhiteSpace(phoneNumberId) && provider.Numbers.Count != 1)
            return new Error("Provider.NumberRequired", "Select a WhatsApp number for this operation.", ErrorType.Validation);
        var number = string.IsNullOrWhiteSpace(phoneNumberId) ? provider.Numbers.Single()
            : provider.Numbers.SingleOrDefault(x => x.PhoneNumberId == phoneNumberId);
        if (number is null) return new Error("Provider.NotFound", "The WhatsApp number was not found in this branch.", ErrorType.NotFound);
        if (string.IsNullOrWhiteSpace(number.SystemUserAccessToken) || number.AccessTokenExpiresAt <= DateTime.UtcNow)
            return new Error("Provider.TokenUnavailable", "Configure a valid access token for this number.", ErrorType.Validation);
        // Resolution identifies credentials only. Sending services must separately enforce registration,
        // consent, the customer-service window and message delivery rules.
        return new ProviderNumberMatch(provider, number);
    }
}
