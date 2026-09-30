using CommonServices.WorkContext.ContextAccessor;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace CommonServices.WorkContext.ContextHolderMiddleware;
public sealed class TenantWorkContextMiddleware
{
    private readonly RequestDelegate _next;
    public TenantWorkContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantWorkContextResolver resolver, TenantWorkContextHolder holder)
    {
        if (context.User.Identity?.IsAuthenticated != true)
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
            holder.Set(await resolver.ResolveAsync(userId, tenantId, selected, context.RequestAborted));
        }
        catch (UnauthorizedAccessException)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        await _next(context);
    }
}
