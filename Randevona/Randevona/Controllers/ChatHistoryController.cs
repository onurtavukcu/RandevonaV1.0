using CommonServices.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Randevona.Controllers;

public class ChatHistoryController : Controller
{
    [HttpGet]
    [WorkspacePage]
    public IActionResult Index() => View();
}
