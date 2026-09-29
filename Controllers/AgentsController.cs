using Microsoft.AspNetCore.Mvc;
using RushMyBookings.Crm.Services;
using RushMyBookings.Crm.ViewModels;

namespace RushMyBookings.Crm.Controllers;

public class AgentsController(ICrmDataService dataService) : CrmControllerBase(dataService)
{
    public async Task<IActionResult> Index()
    {
        var agents = await DataService.GetAgentsAsync();
        return View(new AgentsIndexViewModel { Agents = agents });
    }
    public async Task<IActionResult> AgentManagement(
    DateTime? fromDate,
    DateTime? toDate)
    {
        var agents = await DataService.GetAgentPerformanceAsync(fromDate, toDate);

        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

        return View(agents);
    }
}
