using CommonServices.Authorization;
using Domain.Models.Identity.Management;
using Domain.Models.Shared.Result;
using IdentityService.ManagementService;
using Microsoft.AspNetCore.Mvc;
using Randevona.Models.Management;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Randevona.Controllers;

[PlatformAdmin]
[Route("management")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ManagementController(IManagementService management) : Controller
{
    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "";
    [HttpGet("")]
    public IActionResult Index() => View();

    [HttpGet("tenants/{id}/workspace")]
    public async Task<IActionResult> Workspace(string id, CancellationToken ct)
    {
        var result = await management.GetWorkspaceAsync(ActorId, id, ct);
        return result.IsSuccess ? View(result.Value) : Failure(result.Error!);
    }

    [HttpPost("tenants/{id}/workspace")]
    public async Task<IActionResult> SelectWorkspace(string id, string? organizationId, CancellationToken ct)
    {
        var ticket = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!ticket.Succeeded || ticket.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) != ActorId)
            return StatusCode(StatusCodes.Status403Forbidden);
        var result = await management.SelectWorkspaceAsync(ActorId, id, organizationId ?? "", ct);
        if (!result.IsSuccess)
        {
            var options = await management.GetWorkspaceAsync(ActorId, id, ct);
            if (!options.IsSuccess) return Failure(options.Error!);
            Response.StatusCode = Status(result.Error!);
            ViewData["SelectionError"] = result.Error!.Message;
            return View(nameof(Workspace), options.Value);
        }
        await WriteWorkspaceTicket(ticket, result.Value!.TenantId, result.Value.OrganizationId);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost("workspace/clear")]
    public async Task<IActionResult> ClearWorkspace()
    {
        var ticket = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!ticket.Succeeded || ticket.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) != ActorId)
            return StatusCode(StatusCodes.Status403Forbidden);
        await WriteWorkspaceTicket(ticket, null, null);
        return RedirectToAction(nameof(Index));
    }

    private Task WriteWorkspaceTicket(AuthenticateResult ticket, string? tenantId, string? organizationId)
    {
        var identity = new ClaimsIdentity(ticket.Principal!.Claims.Where(x =>
            x.Type != AdminWorkspaceClaims.TenantId && x.Type != AdminWorkspaceClaims.OrganizationId), CookieAuthenticationDefaults.AuthenticationScheme);
        if (tenantId is not null && organizationId is not null)
        {
            identity.AddClaim(new Claim(AdminWorkspaceClaims.TenantId, tenantId));
            identity.AddClaim(new Claim(AdminWorkspaceClaims.OrganizationId, organizationId));
        }
        // Retain the actor, role, persistence preference and original session expiry.
        return HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), ticket.Properties);
    }

    [HttpGet("tenants")]
    public async Task<IActionResult> Tenants([FromQuery] ManagementQuery query, CancellationToken ct, bool selectionUnavailable = false)
    {
        if (selectionUnavailable) ViewData["WorkspaceError"] = "Your selected business or branch is no longer available. Choose another business or stop managing the current one.";
        if (!ModelState.IsValid) return InvalidFilter();
        var result = await management.GetTenantsAsync(ActorId, query, ct);
        return result.IsSuccess ? View(result.Value) : Failure(result.Error!);
    }
    [HttpGet("tenants/{id}")]
    public async Task<IActionResult> TenantDetails(string id, CancellationToken ct)
    {
        var result = await management.GetTenantAsync(ActorId, id, ct);
        return result.IsSuccess ? View(result.Value) : Failure(result.Error!);
    }
    [HttpGet("users")]
    public async Task<IActionResult> Users([FromQuery] ManagementQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid) return InvalidFilter();
        var result = await management.GetUsersAsync(ActorId, query, ct);
        return result.IsSuccess ? View(result.Value) : Failure(result.Error!);
    }
    [HttpGet("users/{id}")]
    public async Task<IActionResult> UserDetails(string id, CancellationToken ct)
    {
        var result = await management.GetUserAsync(ActorId, id, ct);
        return result.IsSuccess ? View(result.Value) : Failure(result.Error!);
    }
    [HttpPost("users/{id}/review")]
    public async Task<IActionResult> Review(string id, ReviewApplicationModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return await ReviewFailure(id, new Error("Management.InvalidReview", "Check the decision and rejection reason (maximum 1,000 characters).", ErrorType.Validation), model.Reason, ct);
        var result = await management.ReviewAsync(ActorId, id, model.Decision, model.Reason, ct);
        if (!result.IsSuccess) return await ReviewFailure(id, result.Error!, model.Reason, ct);
        TempData["ManagementNotice"] = model.Decision == ApplicationDecision.Approve
            ? "Application approved. The user can now sign in." : "Application rejected. The user cannot sign in.";
        return RedirectToAction(nameof(UserDetails), new { id });
    }
    private async Task<IActionResult> ReviewFailure(string id, Error error, string? reason, CancellationToken ct)
    {
        if (error.Code == "Management.Forbidden") return Failure(error);
        var details = await management.GetUserAsync(ActorId, id, ct);
        if (!details.IsSuccess) return Failure(details.Error!);
        Response.StatusCode = Status(error);
        ViewData["ReviewError"] = error.Message;
        ViewData["ReviewReason"] = reason;
        return View(nameof(UserDetails), details.Value);
    }
    private IActionResult InvalidFilter() => Failure(new Error("Management.InvalidQuery", "Check the search, status and page filters.", ErrorType.Validation));
    private IActionResult Failure(Error error)
    {
        Response.StatusCode = Status(error);
        return View("ManagementError", error);
    }
    private static int Status(Error error) => error.Code == "Management.Forbidden" ? 403 : error.Type switch
    { ErrorType.NotFound => 404, ErrorType.Conflict => 409, ErrorType.Validation => 400, _ => 503 };

    [HttpGet("platform-settings")]
    public IActionResult PlatformSettings() => View();
    [HttpGet("package-settings")]
    public IActionResult PackageSettings() => View();
    [HttpGet("reports")]
    public IActionResult Reports() => View();
}
