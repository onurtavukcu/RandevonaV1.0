using BussinessServices.ProviderService;
using CommonServices.Authorization;
using CommonServices.WorkContext.ContextHolderMiddleware;
using Domain.Models.Meta;
using Domain.Models.Shared.Result;
using Microsoft.AspNetCore.Mvc;
using Randevona.Models.Connections;

namespace Randevona.Controllers;

[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ConnectionsController(IProviderService providers, TenantWorkContextHolder holder) : Controller
{
    [HttpGet]
    [WorkspacePage]
    public async Task<IActionResult> Index(string? phoneNumberId, CancellationToken ct)
    {
        if (holder.Value is null) return View(new ConnectionsViewModel());
        return await Page(phoneNumberId, null, ct);
    }

    [HttpPost]
    [TenantOperation]
    [RequestSizeLimit(32768)]
    public async Task<IActionResult> Save(SaveConnectionRequest model, CancellationToken ct)
    {
        var valid = ModelState.IsValid;
        // Never reflect submitted credentials, even after a model-binding or service failure.
        ModelState.Clear();
        if (!valid) return await Page(model.PhoneNumberId, new Error("Connections.Invalid", "Check the form values and the UTC expiry date.", ErrorType.Validation), ct);
        if (model.AccessTokenExpiresAt is { } expiry)
            model.AccessTokenExpiresAt = DateTime.SpecifyKind(expiry, DateTimeKind.Utc);
        var result = await providers.SaveAsync(model, ct);
        model.AccessToken = null;
        if (!result.IsSuccess) return await Page(model.PhoneNumberId, result.Error, ct);
        TempData["ConnectionNotice"] = "Connection details saved. Meta verification has not been performed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [TenantOperation]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> CheckAccess(VerifyConnectionRequest model, CancellationToken ct)
    {
        var valid = ModelState.IsValid; ModelState.Clear();
        if (!valid) return await Page(null, new Error("Connections.Invalid", "Reload Connections before checking access.", ErrorType.Validation), ct);
        var result = await providers.CheckAccessAsync(model, ct);
        if (!result.IsSuccess) return await Page(model.PhoneNumberId, result.Error, ct);
        TempData["ConnectionNotice"] = "Meta account access checked successfully. Number registration and messaging readiness are separate checks.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> Page(string? phoneNumberId, Error? error, CancellationToken ct)
    {
        var loaded = await providers.GetAsync(ct);
        error ??= loaded.Error;
        if (error is not null) Response.StatusCode = error.Code == "Connections.Forbidden" ? 403 : error.Type switch
        { ErrorType.Conflict => 409, ErrorType.Validation => 400, ErrorType.NotFound => 404, _ => 503 };
        return View(nameof(Index), new ConnectionsViewModel
        {
            Connections = loaded.Value, Error = error?.Message,
            Editing = loaded.Value?.Numbers.FirstOrDefault(x => x.PhoneNumberId == phoneNumberId)
        });
    }
}
