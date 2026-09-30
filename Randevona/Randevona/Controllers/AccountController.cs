using Domain.Models.Identity.User.Login;
using Domain.Models.Identity.User.Register;
using Domain.Models.Identity.User.Settings;
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
public class AccountController(IRegisterService registerService, ILoginService loginService, JwtSettings jwtSettings) : Controller
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
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        model.ReturnUrl = LocalReturnUrl(model.ReturnUrl);
        ModelState.Remove(nameof(model.ReturnUrl));

        if (ModelState.IsValid)
        {
            var result = await loginService.LoginAsync(new LoginRequest
            {
                Email = model.Email,
                Password = model.Password
            });

            if (result.IsSuccess)
            {
                var loginResponse = result.Value;

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, loginResponse.UserId),
                    new Claim(ClaimTypes.Email, loginResponse.Email),
                    new Claim(ClaimTypes.Name, loginResponse.Email),
                    new Claim("tenantId", loginResponse.TenantId),
                    new Claim(ClaimTypes.Role, loginResponse.Role)
                };

                var identity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme);

                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = false,
                        AllowRefresh = true
                    });

                Response.Cookies.Append(
                            "token",
                            loginResponse.Token,
                            new CookieOptions
                            {
                                HttpOnly = true,
                                Secure = true,
                                SameSite = SameSiteMode.Strict,
                                Expires = DateTimeOffset.UtcNow.AddMinutes(jwtSettings.DurationInMinutes)
                            });

                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, result.Error!.Message);
        }

        model.Password = string.Empty;
        ClearAttemptedPassword(nameof(model.Password));

        return View(model);
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
