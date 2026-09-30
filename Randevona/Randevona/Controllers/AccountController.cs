using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Randevona.Models.Account;

namespace Randevona.Controllers;

[AllowAnonymous]
[Route("account")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AccountController : Controller
{
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        // Tag helpers prefer ModelState over the model; discard the raw query value as well.
        ModelState.Remove(nameof(LoginViewModel.ReturnUrl));
        return View(new LoginViewModel { ReturnUrl = LocalReturnUrl(returnUrl) });
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public IActionResult Login(LoginViewModel model)
    {
        model.ReturnUrl = LocalReturnUrl(model.ReturnUrl);
        ModelState.Remove(nameof(model.ReturnUrl));
        if (ModelState.IsValid)
        {
            // TODO: Call LoginService, then sign in with the cookie scheme explicitly.
            ModelState.AddModelError(string.Empty, "Giriş henüz kullanıma açılmadı. Oturum oluşturulmadı.");
        }
        model.Password = string.Empty;
        ClearAttemptedPassword(nameof(model.Password));
        return View(model);
    }

    [HttpGet("register")]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    public IActionResult Register(RegisterViewModel model)
    {
        if (ModelState.IsValid)
        {
            // TODO: Call RegisterService. Tenant, role and database identity are assigned by the service.
            ModelState.AddModelError(string.Empty, "Kayıt henüz kullanıma açılmadı. Hesap oluşturulmadı.");
        }
        model.Password = model.ConfirmPassword = string.Empty;
        ClearAttemptedPassword(nameof(model.Password));
        ClearAttemptedPassword(nameof(model.ConfirmPassword));
        return View(model);
    }

    private string? LocalReturnUrl(string? value) => Url.IsLocalUrl(value) ? value : null;

    private void ClearAttemptedPassword(string field)
    {
        // Keep validation errors, but never send a submitted password back in the HTML.
        if (ModelState.TryGetValue(field, out var entry))
        {
            entry.RawValue = null;
            entry.AttemptedValue = null;
        }
    }
}
