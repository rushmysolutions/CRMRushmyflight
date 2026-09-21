using Microsoft.AspNetCore.Mvc;
using RushMyBookings.Crm.Services;
using RushMyBookings.Crm.ViewModels;



namespace RushMyBookings.Crm.Controllers
{
    public class CustomerController(ICrmDataService dataService) : CrmControllerBase(dataService)
    {
        public IActionResult CustomerManagement()
        {
            return View();
        }
    }
}
