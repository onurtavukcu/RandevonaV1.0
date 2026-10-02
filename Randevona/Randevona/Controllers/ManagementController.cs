using CommonServices.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Randevona.Controllers;

[PlatformAdmin]
[Route("management")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ManagementController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();

    [HttpGet("platform-settings")]
    public IActionResult PlatformSettings() => View();

    [HttpGet("package-settings")]
    public IActionResult PackageSettings() => View();

    [HttpGet("reports")]
    public IActionResult Reports() => View();
}
