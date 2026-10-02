using CommonServices.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Randevona.Controllers;

public class TemplateController : Controller
{
    [HttpGet]
    [WorkspacePage]
    public IActionResult TemplateList() => View();

    [HttpGet]
    [WorkspacePage]
    public IActionResult CreateTemplate() => View();
}
