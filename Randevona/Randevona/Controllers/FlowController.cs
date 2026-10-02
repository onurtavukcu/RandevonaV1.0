using CommonServices.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Randevona.Controllers;

public class FlowController : Controller
{
    [HttpGet]
    [WorkspacePage]
    public IActionResult CreateFlow() => View();

    [HttpGet]
    [WorkspacePage]
    public IActionResult AllFlows() => View();

    [HttpGet]
    [WorkspacePage]
    public IActionResult Operations() => View();

    [HttpGet]
    [WorkspacePage]
    public IActionResult DataSources() => View();
}
