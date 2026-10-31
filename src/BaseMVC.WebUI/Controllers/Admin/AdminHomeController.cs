using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaseMVC.WebUI.Controllers.Admin;

[Route("Admin")]
public sealed class AdminHomeController : Controller
{
    [Authorize]
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Admin/Home/Index.cshtml");
}
