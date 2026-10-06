using Domain.Models.Shared.WorkContext;
namespace CommonServices.WorkContext.ContextAccessor;
public interface ITenantWorkContextResolver
{
    Task ValidatePlatformAdminAsync(string userId, CancellationToken ct);
    Task<Domain.Models.Identity.Management.AdminWorkspaceOptions> GetAdminWorkspaceAsync(string userId, string tenantId, CancellationToken ct);
    Task<TenantWorkContext> ResolveAdminAsync(string userId, string tenantId, string organizationId, CancellationToken ct);
    Task<TenantWorkContext> ResolveAsync(string userId, string tenantId, string? selectedOrganizationId, CancellationToken ct);
}
