using CommonServices.Authorization;
using Domain.Models.Identity.Management;
using Domain.Models.Shared.Result;
using IdentityService.ManagementService;
using Microsoft.AspNetCore.Mvc;
using Randevona.Models.Management;
using System.Security.Claims;

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

    [HttpGet("tenants")]
    public async Task<IActionResult> Tenants([FromQuery] ManagementQuery query, CancellationToken ct)
    {
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
