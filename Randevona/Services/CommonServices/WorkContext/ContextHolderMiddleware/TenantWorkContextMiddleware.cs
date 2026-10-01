using CommonServices.WorkContext.ContextAccessor;
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
        var tenantId = context.User.FindFirst("tenantId")?.Value;
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(tenantId))
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
