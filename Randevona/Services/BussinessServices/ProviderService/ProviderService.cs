using CommonServices.WorkContext.ContextAccessor;
using Data.Repositories.Whatsapp;
using Domain.Entities.Whatsapp;
using Domain.Models.Identity.User.UserInformation;
using Domain.Models.Meta;
using Domain.Models.Shared.Result;
using Domain.Models.Shared.WorkContext;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using IntegrationServices.Whatsapp;

namespace BussinessServices.ProviderService;

public sealed class ProviderService(IProviderDataRepository repository, ITenantContextAccessor accessor,
    ITenantWorkContextResolver resolver, IMetaPhoneClient meta, ILogger<ProviderService> logger) : IProviderService
{
    public Task<Result<ConnectionPage>> GetAsync(CancellationToken ct = default) => ExecuteAsync<ConnectionPage>(async scope =>
    {
        var provider = await repository.GetAsync(ct);
        return new ConnectionPage(scope.TenantId, scope.OrganizationId, provider?.Version ?? 0,
            provider?.Numbers.Select(x => new ConnectionNumber(x.PhoneNumberId, x.WabaId, x.PhoneNumber,
                x.MetaAppId, x.Status, !string.IsNullOrEmpty(x.SystemUserAccessToken), x.AccessTokenExpiresAt, x.LastSyncedAt)).ToArray() ?? []);
    }, ct);

    public Task<Result<bool>> SaveAsync(SaveConnectionRequest request, CancellationToken ct = default) => ExecuteAsync<bool>(async scope =>
    {
        // Protect old tabs/forms after changing the selected business or branch.
        if (request.ExpectedTenantId != scope.TenantId || request.ExpectedOrganizationId != scope.OrganizationId)
            return new Error("Connections.WorkspaceChanged", "Your selected business or branch changed. Reload Connections before saving.", ErrorType.Conflict);
        if (request.ExpectedVersion < 0 || request.ExpectedVersion == long.MaxValue)
            return Invalid("The connection version is invalid.");
        var phoneId = request.PhoneNumberId?.Trim() ?? "";
        var wabaId = request.WabaId?.Trim() ?? "";
        var appId = request.MetaAppId?.Trim() ?? "";
        var phone = request.PhoneNumber?.Trim() ?? "";
        var token = request.AccessToken?.Trim();
        if (!MetaId(phoneId) || !MetaId(wabaId) || !MetaId(appId))
            return Invalid("Phone number ID, WhatsApp Business Account ID and Meta app ID must contain 5 to 30 digits.");
        if (!Regex.IsMatch(phone, @"\A\+[1-9][0-9]{6,14}\z"))
            return Invalid("Enter the phone number in international format, for example +905551234567.");
        if (token is not null && (token.Length > 8192 || token.Any(char.IsWhiteSpace)))
            return Invalid("The access token must contain no whitespace and at most 8,192 characters.");
        if (request.AccessTokenExpiresAt is { } expiry && (expiry.Kind != DateTimeKind.Utc || expiry <= DateTime.UtcNow))
            return Invalid("Token expiry must be a future UTC date and time.");
        var provider = await repository.GetAsync(ct);
        if ((provider?.Version ?? 0) != request.ExpectedVersion)
            return Conflict();
        var existing = provider?.Numbers.SingleOrDefault(x => x.PhoneNumberId == phoneId);
        if (existing is not null && (existing.WabaId != wabaId || existing.MetaAppId != appId))
            return Invalid("This number is already configured with a different account or app. Ownership changes require a separate migration.");
        if (existing is null && string.IsNullOrEmpty(token))
            return Invalid("An access token is required for a new number.");
        if (existing is null && (provider?.Numbers.Count ?? 0) >= 20)
            return Invalid("A branch can have at most 20 configured numbers.");
        if (string.IsNullOrEmpty(token) && existing is not null && request.AccessTokenExpiresAt != existing.AccessTokenExpiresAt)
            return Invalid("Provide a replacement token to change its expiry.");

        provider ??= new ProviderData { TenantId = scope.TenantId, OrganizationId = scope.OrganizationId,
            ConnectedByUserId = scope.UserId, DisplayName = scope.OrganizationName ?? "WhatsApp" };
        ProviderNumberDirectory? directory = null;
        if (existing is null)
        {
            existing = new WhatsAppProviderData { PhoneNumberId = phoneId, WabaId = wabaId, MetaAppId = appId };
            provider.Numbers.Add(existing);
            directory = new ProviderNumberDirectory { TenantId = scope.TenantId, OrganizationId = scope.OrganizationId,
                PhoneNumberId = phoneId, WabaId = wabaId, ProviderDataId = provider.Id };
        }
        existing.PhoneNumber = phone;
        if (!string.IsNullOrEmpty(token))
        {
            existing.SystemUserAccessToken = token;
            existing.AccessTokenExpiresAt = request.AccessTokenExpiresAt;
        }
        // Saving credentials is not evidence of a successful Meta connection.
        existing.Status = "Configured";
        existing.IsRegistered = false;
        existing.LastSyncedAt = null;
        provider.Status = ConnectionStatus.Configured;
        provider.Version = request.ExpectedVersion + 1;
        provider.UpdatedByUserId = scope.UserId;
        provider.UpdatedAt = DateTime.UtcNow;
        if (!await repository.TrySaveAsync(provider, request.ExpectedVersion, directory, ct)) return Conflict();
        return true;
    }, ct);

    public Task<Result<bool>> CheckAccessAsync(VerifyConnectionRequest request, CancellationToken ct = default) => ExecuteAsync<bool>(async scope =>
    {
        if (request.ExpectedTenantId != scope.TenantId || request.ExpectedOrganizationId != scope.OrganizationId)
            return new Error("Connections.WorkspaceChanged", "Your selected business or branch changed. Reload Connections before checking access.", ErrorType.Conflict);
        if (request.ExpectedVersion < 1 || request.ExpectedVersion == long.MaxValue) return Invalid("The connection version is invalid.");
        var provider = await repository.GetAsync(ct);
        if (provider is null || provider.Version != request.ExpectedVersion) return Conflict();
        var number = provider.Numbers.SingleOrDefault(x => x.PhoneNumberId == request.PhoneNumberId);
        if (number is null) return Invalid("The number does not belong to the selected branch.");
        if (number.AccessTokenExpiresAt <= DateTime.UtcNow) return Invalid("Replace the expired access token before checking access.");
        var result = await meta.CheckAccessAsync(number.WabaId, number.PhoneNumberId, number.SystemUserAccessToken, ct);
        if (!result.IsSuccess) return result.Error!;
        var digits = new string(result.Value!.DisplayPhoneNumber.Where(c => c is >= '0' and <= '9').ToArray());
        if ("+" + digits != number.PhoneNumber)
            return Invalid("Meta's phone number differs from the stored number. Correct the number before checking access.");
        // Revalidate after the external call, before saving its result.
        if (scope.SystemUserRoleType == SystemUserRoleType.SuperAdmin)
            await resolver.ResolveAdminAsync(scope.UserId, scope.TenantId, scope.OrganizationId, ct);
        else await resolver.ResolveAsync(scope.UserId, scope.TenantId, scope.OrganizationId, ct);
        number.Status = "Access verified"; number.LastSyncedAt = DateTime.UtcNow;
        number.VerifiedName = result.Value.VerifiedName;
        // Listing a number does not prove Cloud API registration or permission to send a message.
        provider.Status = provider.Numbers.All(x => x.Status == "Access verified") ? ConnectionStatus.AccessVerified : ConnectionStatus.Configured;
        provider.Version++; provider.UpdatedAt = DateTime.UtcNow; provider.UpdatedByUserId = scope.UserId;
        return await repository.TrySaveAsync(provider, request.ExpectedVersion, null, ct) ? true : Conflict();
    }, ct);

    private async Task<Result<T>> ExecuteAsync<T>(Func<TenantWorkContext, Task<Result<T>>> action, CancellationToken ct)
    {
        try
        {
            var scope = accessor.CurrentWorkContext;
            var current = scope.SystemUserRoleType == SystemUserRoleType.SuperAdmin
                ? await resolver.ResolveAdminAsync(scope.UserId, scope.TenantId, scope.OrganizationId, ct)
                : await resolver.ResolveAsync(scope.UserId, scope.TenantId, scope.OrganizationId, ct);
            if (current.SystemUserRoleType != scope.SystemUserRoleType || string.IsNullOrEmpty(current.OrganizationId))
                throw new UnauthorizedAccessException();
            return await action(current);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (UnauthorizedAccessException) { return new Error("Connections.Forbidden", "Access to this business and branch is unavailable.", ErrorType.Validation); }
        catch (Exception ex)
        {
            // Never include exception messages or request/provider objects: they may contain tokens.
            logger.LogError("Connection operation failed ({ErrorType}).", ex.GetType().Name);
            return new Error("Connections.Failed", "The connection could not be saved or loaded. Refresh to check its current state before retrying.", ErrorType.Failure);
        }
    }
    private static bool MetaId(string value) => Regex.IsMatch(value, @"\A[0-9]{5,30}\z");
    private static Error Invalid(string message) => new("Connections.Invalid", message, ErrorType.Validation);
    private static Error Conflict() => new("Connections.Conflict", "The connection changed or this number is already assigned. Reload Connections and check its owner before retrying.", ErrorType.Conflict);
}
