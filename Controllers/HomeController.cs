using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RushMyBookings.Crm.Services;
using RushMyBookings.Crm.ViewModels;

namespace RushMyBookings.Crm.Controllers;

public class HomeController(ICrmDataService dataService) : CrmControllerBase(dataService)
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            return View(new DashboardViewModel
            {
                DatabaseConnected = true,
                Stats = await DataService.GetDashboardStatsAsync(cancellationToken)
            });
        }
        catch
        {
            return View(new DashboardViewModel { DatabaseConnected = false });
        }
    }

    [AllowAnonymous]
    public IActionResult Error()
    {
        return View();
    }
}
