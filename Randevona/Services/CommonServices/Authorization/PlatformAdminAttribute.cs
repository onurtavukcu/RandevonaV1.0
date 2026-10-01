using Domain.Models.Identity.User.UserInformation;
using Microsoft.AspNetCore.Authorization;

namespace CommonServices.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class PlatformAdminAttribute : AuthorizeAttribute
{
    public PlatformAdminAttribute() => Roles = nameof(SystemUserRoleType.SuperAdmin);
}
