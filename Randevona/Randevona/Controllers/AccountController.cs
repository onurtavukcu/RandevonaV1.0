using Domain.Models.Identity.User.Login;
using Domain.Models.Identity.User.Register;
using Domain.Models.Identity.User.UserInformation;
using IdentityService.LoginService;
using IdentityService.RegisterService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Randevona.Models.Account;
using System.Security.Claims;

namespace Randevona.Controllers;

[AllowAnonymous]
[Route("account")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AccountController(IRegisterService registerService, ILoginService loginService) : Controller
{
    [HttpGet("/")]
    public IActionResult Start() => RedirectToAction(nameof(Login));

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        // Tag helpers prefer ModelState over the model; discard the raw query value as well.
        ModelState.Remove(nameof(LoginViewModel.ReturnUrl));
        return View(new LoginViewModel { ReturnUrl = LocalReturnUrl(returnUrl) });
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        model.ReturnUrl = LocalReturnUrl(model.ReturnUrl);
        ModelState.Remove(nameof(model.ReturnUrl));
        if (ModelState.IsValid)
        {
            var result = await loginService.LoginAsync(new LoginRequest { Email = model.Email, Password = model.Password }, ct);
            if (result.IsSuccess)
            {
                var response = result.Value!;
                var identity = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, response.UserId),
                    new Claim(ClaimTypes.Email, response.Email),
                    new Claim(ClaimTypes.Name, response.Email),
                    new Claim(ClaimTypes.Role, response.Role)
                }, CookieAuthenticationDefaults.AuthenticationScheme);
                if (response.TenantId is not null) identity.AddClaim(new Claim("tenantId", response.TenantId));
                if (response.OrganizationId is not null) identity.AddClaim(new Claim("organizationId", response.OrganizationId));
                ClearAccountCookies();
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
                    new AuthenticationProperties
                    {
                        IsPersistent = model.RememberMe, AllowRefresh = true,
                        IssuedUtc = response.IssuedAtUtc, ExpiresUtc = response.ExpiresAtUtc
                    });
                TempData.Remove("RegistrationSubmitted");
                if (response.Role == nameof(SystemUserRoleType.SuperAdmin))
                    return RedirectToAction("Index", "Management");
                if (model.ReturnUrl is not null) return LocalRedirect(model.ReturnUrl);
                return RedirectToAction("Index", "Home");
            }
            ModelState.AddModelError(string.Empty, result.Error!.Message);
        }
        model.Password = string.Empty;
        ClearAttemptedPassword(nameof(model.Password));
        return View(model);
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        ClearAccountCookies();
        TempData.Clear();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("access-denied")]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    private void ClearAccountCookies()
    {
        Response.Cookies.Delete("ActiveOrgId", new CookieOptions { Path = "/" });
        Response.Cookies.Delete("SelectedOrgId", new CookieOptions { Path = "/" });
        // Clean up the old JWT cookie. Web authentication uses the protected ASP.NET cookie.
        Response.Cookies.Delete("token", new CookieOptions { Path = "/", Secure = true, SameSite = SameSiteMode.Strict });
    }
    [HttpGet("register")]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await registerService.RegisterAsync(new RegisterRequest
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                CompanyName = model.CompanyName,
                OrganizationName = model.OrganizationName,
                Email = model.Email,
                Password = model.Password,
                ConfirmPassword = model.ConfirmPassword
            }, ct);
            if (result.IsSuccess)
            {
                TempData["RegistrationSubmitted"] = true;
                return RedirectToAction(nameof(RegistrationReceived));
            }
            ModelState.AddModelError(string.Empty, result.Error!.Message);
        }
        model.Password = model.ConfirmPassword = string.Empty;
        ClearAttemptedPassword(nameof(model.Password));
        ClearAttemptedPassword(nameof(model.ConfirmPassword));
        return View(model);
    }

    [HttpGet("register/received")]
    public IActionResult RegistrationReceived()
    {
        if (TempData.Peek("RegistrationSubmitted") is not true) return RedirectToAction(nameof(Register));
        return View();
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

