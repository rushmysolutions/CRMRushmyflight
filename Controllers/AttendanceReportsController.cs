using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RushMyBookings.Crm.Helpers;
using RushMyBookings.Crm.Services;
using RushMyBookings.Crm.ViewModels.Attendance;

namespace RushMyBookings.Crm.Controllers;

[Authorize]
public class AttendanceReportsController(IAttendanceService attendance) : Controller
{
    private IActionResult? RequireAttendanceEmployee()
    {
        if (!AttendanceAccess.HasAttendanceAccess(User))
        {
            return View("~/Views/Attendance/NoAccess.cshtml");
        }

        return null;
    }

    [HttpGet]
    public async Task<IActionResult> Team(DateOnly? date, int? teamId, string? q)
    {
        var denied = RequireAttendanceEmployee();
        if (denied is not null)
        {
            return denied;
        }

        if (!AttendanceAccess.CanViewTeam(User))
        {
            return Forbid();
        }

        var viewerId = AttendanceAccess.GetEmployeeId(User)!.Value;
        var canViewAll = AttendanceAccess.CanViewAll(User);
        var day = date ?? DateOnly.FromDateTime(DateTime.Today);

        var vm = new TeamAttendanceViewModel
        {
            Date = day,
            TeamId = teamId,
            Search = q?.Trim(),
            Teams = canViewAll ? await attendance.GetTeamsAsync() : [],
            Rows = await attendance.GetTeamAttendanceAsync(viewerId, teamId, day, canViewAll, q)
        };

        ViewData["Title"] = "Team Attendance";
        ViewData["ActiveNav"] = "attendance-team";
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Monthly(int? year, int? month, int? employeeId, string? q)
    {
        var denied = RequireAttendanceEmployee();
        if (denied is not null)
        {
            return denied;
        }

        var viewerId = AttendanceAccess.GetEmployeeId(User)!.Value;
        var canViewAll = AttendanceAccess.CanViewAll(User);
        var canViewTeam = AttendanceAccess.CanViewTeam(User);
        var (y, m) = AttendancePeriodHelper.Clamp(year, month);

        // Employee without team/all rights can only see self
        if (!canViewAll && !canViewTeam)
        {
            employeeId = viewerId;
        }

        IReadOnlyList<EmployeeOption> employeeOptions = [];
        if (canViewAll || canViewTeam)
        {
            var all = await attendance.GetEmployeesAsync(null);
            employeeOptions = all
                .Where(e => e.IsActive)
                .Select(e => new EmployeeOption
                {
                    EmployeeId = e.EmployeeId,
                    Display = $"{e.EmpCode} - {e.FullName}"
                })
                .ToList();
        }

        var rows = await attendance.GetMonthlySummariesAsync(
            y, m, employeeId, viewerId, canViewAll, canViewTeam, q);

        var vm = new MonthlyReportViewModel
        {
            Year = y,
            Month = m,
            EmployeeId = employeeId,
            Search = q?.Trim(),
            Employees = employeeOptions,
            Rows = rows,
            DayNumbers = Enumerable.Range(1, DateTime.DaysInMonth(y, m)).ToList(),
            CanCloseMonth = AttendanceAccess.CanCloseMonth(User)
        };

        ViewBag.AllowedMonths = AttendancePeriodHelper.GetAllowedMonths();
        ViewData["Title"] = "Monthly Report";
        ViewData["ActiveNav"] = "attendance-reports";
        return View(vm);
    }

    [HttpGet]
    public IActionResult CloseMonth()
    {
        if (!AttendanceAccess.CanCloseMonth(User) || !AttendanceAccess.HasAttendanceAccess(User))
        {
            return Forbid();
        }

        var allowed = AttendancePeriodHelper.GetAllowedMonths();
        // Default to previous month when available, else current
        var selected = allowed.Count > 1 ? allowed[1] : allowed[0];

        ViewData["Title"] = "Close Month";
        ViewData["ActiveNav"] = "attendance-close";
        ViewBag.AllowedMonths = allowed;
        return View(new CloseMonthViewModel
        {
            Year = selected.Year,
            Month = selected.Month
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseMonth(CloseMonthViewModel model)
    {
        if (!AttendanceAccess.CanCloseMonth(User) || !AttendanceAccess.HasAttendanceAccess(User))
        {
            return Forbid();
        }

        if (!AttendancePeriodHelper.IsAllowed(model.Year, model.Month))
        {
            TempData["AttendanceError"] = "You can only close the current month or the previous month.";
            return RedirectToAction(nameof(CloseMonth));
        }

        var actorId = AttendanceAccess.GetEmployeeId(User)!.Value;
        var result = await attendance.CalculateMonthAsync(
            model.Year, model.Month, actorId, model.LockMonth);

        TempData[result.Ok ? "AttendanceSuccess" : "AttendanceError"] = result.Message;
        return RedirectToAction(nameof(CloseMonth));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetEmployeeLock(
        int employeeId,
        int year,
        int month,
        bool lockMonth)
    {
        if (!AttendanceAccess.CanCloseMonth(User) || !AttendanceAccess.HasAttendanceAccess(User))
        {
            return Forbid();
        }

        var actorId = AttendanceAccess.GetEmployeeId(User)!.Value;
        var result = await attendance.SetEmployeeMonthLockAsync(
            employeeId, year, month, lockMonth, actorId);

        TempData[result.Ok ? "AttendanceSuccess" : "AttendanceError"] = result.Message;
        return RedirectToAction(nameof(Monthly), new { year, month, employeeId });
    }
}
