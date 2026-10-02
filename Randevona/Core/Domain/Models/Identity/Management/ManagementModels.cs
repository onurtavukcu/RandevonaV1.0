using Domain.Models.Identity.Tenant;
using Domain.Models.Identity.User.UserInformation;

namespace Domain.Models.Identity.Management;

public sealed class ManagementQuery
{
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public UserStatus? Status { get; set; }
    public string? TenantId { get; set; }
}

public sealed record ManagementPage<T>(IReadOnlyList<T> Items, long TotalCount, ManagementQuery Query)
{
    public const int PageSize = 20;
    public bool HasPrevious => Query.Page > 1;
    public bool HasNext => (long)Query.Page * PageSize < TotalCount;
}

// Only these safe projections are sent to the management views, never the Users entity.
public sealed record ManagedUser(string Id, string Name, string Email, string? TenantId, string? TenantName,
    SystemUserRoleType Role, UserStatus Status, bool IsActive, DateTime CreatedAt,
    string? ReviewedByUserId, DateTime? ReviewedAtUtc, string? RejectionReason)
{
    public bool CanReview => Role == SystemUserRoleType.User && Status == UserStatus.PendingApproval && IsActive;
}

public sealed record ManagedTenant(string Id, string Name, string DatabaseName, string OwnerUserId,
    string InitialOrganizationName, TenantProvisioningStatus ProvisioningStatus, bool IsActive,
    DateTime CreatedAt, ManagedUser? Owner);

public sealed record ManagedUserDetails(ManagedUser User, ManagedTenant? Tenant, ManagedUser? Reviewer,
    bool HasAllOrganizationAccess, int OrganizationCount);

public sealed record ManagedTenantDetails(ManagedTenant Tenant, ManagementPage<ManagedUser> Users);

public enum ApplicationDecision { Approve = 1, Reject = 2 }
