using CommonServices.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Randevona.Controllers;

public class MessageController : Controller
{
    [HttpGet]
    [WorkspacePage]
    public IActionResult SentTemplates() => View();

    [HttpGet]
    [WorkspacePage]
    public IActionResult JobStatus() => View();
}
