using CommonServices.WorkContext.ContextAccessor;
using CommonServices.Authorization;
using Domain.Models.Identity.User.UserInformation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace CommonServices.WorkContext.ContextHolderMiddleware;
public sealed class TenantWorkContextMiddleware
{
    private readonly RequestDelegate _next;
    public TenantWorkContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantWorkContextResolver resolver, TenantWorkContextHolder holder)
    {
        // A revoked or stale session must not prevent reaching login or signing out.
        if (context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null || context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        var platformEndpoint = context.GetEndpoint()?.Metadata.GetMetadata<PlatformAdminAttribute>() is not null;
        var sharedPage = context.GetEndpoint()?.Metadata.GetMetadata<WorkspacePageAttribute>() is not null
            && HttpMethods.IsGet(context.Request.Method);
        if (platformEndpoint || (sharedPage && context.User.IsInRole(nameof(SystemUserRoleType.SuperAdmin))))
        {
            try
            {
                if (!context.User.IsInRole(nameof(SystemUserRoleType.SuperAdmin)))
                    throw new UnauthorizedAccessException("Platform administrator role is required.");
                await resolver.ValidatePlatformAdminAsync(userId, context.RequestAborted);
            }
            catch (UnauthorizedAccessException)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            // No tenant WorkContext: management does not grant implicit access to tenant repositories.
            await _next(context);
            return;
        }
        if (context.User.IsInRole(nameof(SystemUserRoleType.SuperAdmin)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        var tenantId = context.User.FindFirst("tenantId")?.Value;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        var selected = context.Request.Headers["X-Org-Id"].FirstOrDefault()
            ?? context.Request.Cookies["ActiveOrgId"]
            ?? context.Request.Cookies["SelectedOrgId"]
            ?? context.User.FindFirst("organizationId")?.Value;
        try
        {
            var workContext = await resolver.ResolveAsync(userId, tenantId, selected, context.RequestAborted);
            if (context.User.FindFirst(ClaimTypes.Role)?.Value != workContext.SystemUserRoleType.ToString())
                throw new UnauthorizedAccessException("Account role changed; sign in again.");
            holder.Set(workContext);
        }
        catch (UnauthorizedAccessException)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        await _next(context);
    }
}
