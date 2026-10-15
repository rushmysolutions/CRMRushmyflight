using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RushMyBookings.Crm.Controllers;

/// <summary>
/// Welcome page only — keep this thin so post-login redirect stays fast.
/// </summary>
[Authorize]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    [AllowAnonymous]
    public IActionResult Error()
    {
        return View();
    }
}
