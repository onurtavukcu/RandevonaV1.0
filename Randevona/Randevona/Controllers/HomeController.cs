using Microsoft.AspNetCore.Mvc;
using Randevona.Models;
using Microsoft.AspNetCore.Authorization;
using System.Diagnostics;
using CommonServices.Authorization;

namespace Randevona.Controllers
{
    public class HomeController : Controller
    {
        [HttpGet]
        [WorkspacePage]
        public IActionResult Index()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
