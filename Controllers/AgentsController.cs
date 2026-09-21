using Microsoft.AspNetCore.Mvc;
using RushMyBookings.Crm.Services;
using RushMyBookings.Crm.ViewModels;

namespace RushMyBookings.Crm.Controllers;

public class AgentsController(ICrmDataService dataService) : CrmControllerBase(dataService)
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var agents = await DataService.GetAgentsAsync(cancellationToken);
        return View(new AgentsIndexViewModel { Agents = agents });
    }
    public async Task<IActionResult> AgentManagement(
    DateTime? fromDate,
    DateTime? toDate,
    CancellationToken cancellationToken)
    {
        var agents = await DataService.GetAgentPerformanceAsync(fromDate, toDate, cancellationToken);

        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

        return View(agents);
    }
}
