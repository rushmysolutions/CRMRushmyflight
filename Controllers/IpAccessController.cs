using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RushMyBookings.Crm.Helpers;
using RushMyBookings.Crm.Middleware;
using RushMyBookings.Crm.Services;
using RushMyBookings.Crm.ViewModels.Attendance;

namespace RushMyBookings.Crm.Controllers;

[Authorize]
public class IpAccessController(IIpAccessService ipAccess) : Controller
{
    private bool EnsureIpManager() =>
        AttendanceAccess.CanManageIpAddresses(User) && AttendanceAccess.HasAttendanceAccess(User);

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!EnsureIpManager())
        {
            return Forbid();
        }

        ViewData["Title"] = "Allowed IPs";
        ViewData["ActiveNav"] = "attendance-ip";

        var items = await ipAccess.GetAllowedIpsAsync();
        return View(new AllowedIpIndexViewModel
        {
            CurrentIp = IpRestrictionMiddleware.ResolveClientIp(HttpContext),
            Items = items
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string ipAddress, string? note)
    {
        if (!EnsureIpManager())
        {
            return Forbid();
        }

        var result = await ipAccess.AddAsync(
            ipAddress,
            note,
            AttendanceAccess.GetEmployeeId(User));

        TempData[result.Ok ? "AttendanceSuccess" : "AttendanceError"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!EnsureIpManager())
        {
            return Forbid();
        }

        var result = await ipAccess.DeleteAsync(id);
        TempData[result.Ok ? "AttendanceSuccess" : "AttendanceError"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
