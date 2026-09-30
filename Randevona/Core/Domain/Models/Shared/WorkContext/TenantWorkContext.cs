using Domain.Models.Identity.User.UserInformation;

namespace Domain.Models.Shared.WorkContext
{
    public sealed record TenantWorkContext(
         string TenantId,
         string OrganizationId,
         string UserId,
         SystemUserRoleType SystemUserRoleType,
         bool HasAllOrganizationAccess = false,
         IReadOnlyList<string>? AllowedOrganizationIds = null,
         string? PhoneNumberId = null,
         string? HomeTenantId = null);
}
