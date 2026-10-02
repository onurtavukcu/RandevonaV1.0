using CommonServices.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Randevona.Controllers;

public class ContactController : Controller
{
    [HttpGet]
    [WorkspacePage]
    public IActionResult GetAllContact() => View();

    [HttpGet]
    [WorkspacePage]
    public IActionResult ImportContactList() => View();

    [HttpGet]
    [WorkspacePage]
    public IActionResult ExportContactList() => View();
}
