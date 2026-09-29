using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RushMyBookings.Crm.Services;

namespace RushMyBookings.Crm.Controllers;

public class HomeController(ICrmDataService dataService) : CrmControllerBase(dataService)
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
