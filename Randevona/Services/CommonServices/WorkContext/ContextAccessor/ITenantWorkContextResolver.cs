using Domain.Models.Shared.WorkContext;
namespace CommonServices.WorkContext.ContextAccessor;
public interface ITenantWorkContextResolver
{
    Task ValidatePlatformAdminAsync(string userId, CancellationToken ct);
    Task<TenantWorkContext> ResolveAsync(string userId, string tenantId, string? selectedOrganizationId, CancellationToken ct);
}
