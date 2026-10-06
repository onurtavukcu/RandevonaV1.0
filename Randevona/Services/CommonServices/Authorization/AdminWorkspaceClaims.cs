namespace CommonServices.Authorization;

// Only written into the protected web authentication ticket after server-side validation.
public static class AdminWorkspaceClaims
{
    public const string TenantId = "randevona:managedTenantId";
    public const string OrganizationId = "randevona:managedOrganizationId";
}
