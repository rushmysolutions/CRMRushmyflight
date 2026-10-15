using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RushMyBookings.Crm.Services;

namespace RushMyBookings.Crm.Controllers;

[Authorize]
public abstract class CrmControllerBase(ICrmDataService dataService) : Controller
{
    protected ICrmDataService DataService { get; } = dataService;

    // No per-request DB probe. ViewBag.DatabaseConnected was unused in views,
    // and CanConnectAsync hit MySQL every ~1 minute (cache miss) on Home + CRM pages.
}
