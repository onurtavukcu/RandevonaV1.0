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
        var tenantOperation = context.GetEndpoint()?.Metadata.GetMetadata<TenantOperationAttribute>() is not null;
        var sharedPage = context.GetEndpoint()?.Metadata.GetMetadata<WorkspacePageAttribute>() is not null
            && HttpMethods.IsGet(context.Request.Method);
        if (platformEndpoint || ((sharedPage || tenantOperation) && context.User.IsInRole(nameof(SystemUserRoleType.SuperAdmin))))
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
            // Management remains central even when the browser has selected a business.
            if (!platformEndpoint && (sharedPage || tenantOperation))
            {
                var managedTenant = context.User.FindFirst(AdminWorkspaceClaims.TenantId)?.Value;
                var managedOrganization = context.User.FindFirst(AdminWorkspaceClaims.OrganizationId)?.Value;
                if (tenantOperation && (managedTenant is null || managedOrganization is null))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
                if (managedTenant is not null || managedOrganization is not null)
                {
                    try
                    {
                        holder.Set(await resolver.ResolveAdminAsync(userId, managedTenant ?? "", managedOrganization ?? "", context.RequestAborted));
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
                    {
                        // A stale selection must never fall back to another business or branch.
                        if (HttpMethods.IsGet(context.Request.Method))
                            context.Response.Redirect("/management/tenants?selectionUnavailable=true");
                        else context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return;
                    }
                }
            }
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
