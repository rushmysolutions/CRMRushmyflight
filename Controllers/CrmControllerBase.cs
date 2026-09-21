using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RushMyBookings.Crm.Services;

namespace RushMyBookings.Crm.Controllers;

[Authorize]
public abstract class CrmControllerBase(ICrmDataService dataService) : Controller
{
    protected ICrmDataService DataService { get; } = dataService;

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        try
        {
            ViewBag.DatabaseConnected = await DataService.CanConnectAsync(context.HttpContext.RequestAborted);
        }
        catch
        {
            ViewBag.DatabaseConnected = false;
        }

        await base.OnActionExecutionAsync(context, next);
    }
}
