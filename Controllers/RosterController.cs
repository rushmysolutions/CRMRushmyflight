using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RushMyBookings.Crm.Helpers;
using RushMyBookings.Crm.Services;
using RushMyBookings.Crm.ViewModels.Attendance;

namespace RushMyBookings.Crm.Controllers;

/// <summary>
/// Shifts, team/employee schedules, and future week-off roster.
/// SysAdmin / Admin / HR → everyone. Team Lead → their team only.
/// </summary>
[Authorize]
public class RosterController(IAttendanceService attendance) : Controller
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private bool HasAccess() =>
        AttendanceAccess.IsInternalPortal(User) && AttendanceAccess.HasAttendanceAccess(User);

    private bool CanManageAll() => AttendanceAccess.CanManageAllRosters(User);

    private bool CanManageTeam() => AttendanceAccess.CanManageTeamRoster(User);

    private static bool TryParseDay(string? value, out DateOnly day) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", Inv, DateTimeStyles.None, out day)
        || DateOnly.TryParse(value, Inv, DateTimeStyles.None, out day);

    [HttpGet]
    public async Task<IActionResult> Shifts()
    {
        if (!HasAccess() || !CanManageAll())
        {
            return Forbid();
        }

        ViewData["Title"] = "Shifts";
        ViewData["ActiveNav"] = "attendance-roster";
        return View(await attendance.GetShiftsAsync(activeOnly: false));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateShift(string name, string startTime, string endTime)
    {
        if (!HasAccess() || !CanManageAll())
        {
            return Forbid();
        }

        if (!TimeSpan.TryParse(startTime, out var start) || !TimeSpan.TryParse(endTime, out var end))
        {
            TempData["AttendanceError"] = "Enter start/end as HH:mm (e.g. 22:00).";
            return RedirectToAction(nameof(Shifts));
        }

        var result = await attendance.CreateShiftAsync(name, start, end);
        TempData[result.Ok ? "AttendanceSuccess" : "AttendanceError"] = result.Message;
        return RedirectToAction(nameof(Shifts));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateShift(int shiftId, string name, string startTime, string endTime)
    {
        if (!HasAccess() || !CanManageAll())
        {
            return Forbid();
        }

        if (!TimeSpan.TryParse(startTime, out var start) || !TimeSpan.TryParse(endTime, out var end))
        {
            TempData["AttendanceError"] = "Enter start/end as HH:mm (e.g. 22:00).";
            return RedirectToAction(nameof(Shifts));
        }

        var result = await attendance.UpdateShiftAsync(shiftId, name, start, end);
        TempData[result.Ok ? "AttendanceSuccess" : "AttendanceError"] = result.Message;
        return RedirectToAction(nameof(Shifts));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteShift(int shiftId)
    {
        if (!HasAccess() || !CanManageAll())
        {
            return Forbid();
        }

        var result = await attendance.DeleteShiftAsync(shiftId);
        TempData[result.Ok ? "AttendanceSuccess" : "AttendanceError"] = result.Message;
        return RedirectToAction(nameof(Shifts));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetShiftActive(int shiftId, bool isActive)
    {
        if (!HasAccess() || !CanManageAll())
        {
            return Forbid();
        }

        var result = await attendance.SetShiftActiveAsync(shiftId, isActive);
        TempData[result.Ok ? "AttendanceSuccess" : "AttendanceError"] = result.Message;
        return RedirectToAction(nameof(Shifts));
    }

    [HttpGet]
    public async Task<IActionResult> TeamSchedule(int teamId)
    {
        if (!HasAccess() || !CanManageAll())
        {
            return Forbid();
        }

        var model = await attendance.GetTeamScheduleEditAsync(teamId);
        if (model is null)
        {
            return NotFound();
        }

        ViewData["Title"] = "Team schedule";
        ViewData["ActiveNav"] = "attendance-roster";
        return View("ScheduleEdit", model);
    }

    [HttpGet]
    public async Task<IActionResult> EmployeeSchedule(int employeeId)
    {
        if (!HasAccess() || !CanManageAll())
        {
            return Forbid();
        }

        var model = await attendance.GetEmployeeScheduleEditAsync(employeeId);
        if (model is null)
        {
            return NotFound();
        }

        ViewData["Title"] = "Employee schedule";
        ViewData["ActiveNav"] = "attendance-roster";
        return View("ScheduleEdit", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSchedule(ScheduleEditViewModel model)
    {
        if (!HasAccess() || !CanManageAll())
        {
            return Forbid();
        }

        if (model.TeamId.HasValue)
        {
            var result = await attendance.SaveTeamScheduleAsync(model);
            TempData[result.Ok ? "AttendanceSuccess" : "AttendanceError"] = result.Message;
            return RedirectToAction(nameof(TeamSchedule), new { teamId = model.TeamId });
        }

        if (model.EmployeeId.HasValue)
        {
            var result = await attendance.SaveEmployeeScheduleAsync(model);
            TempData[result.Ok ? "AttendanceSuccess" : "AttendanceError"] = result.Message;
            return RedirectToAction(nameof(EmployeeSchedule), new { employeeId = model.EmployeeId });
        }

        TempData["AttendanceError"] = "Nothing to save.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? week, int? teamId)
    {
        if (!HasAccess() || !CanManageTeam())
        {
            return Forbid();
        }

        var viewerId = AttendanceAccess.GetEmployeeId(User)!.Value;
        var manageAll = CanManageAll();
        var weekStart = TryParseDay(week, out var parsed)
            ? parsed
            : DateOnly.FromDateTime(DateTime.Today);

        var vm = await attendance.GetRosterWeekAsync(
            weekStart,
            teamId,
            viewerId,
            manageAll,
            teamLeadOnly: !manageAll);

        ViewData["Title"] = "Roster";
        ViewData["ActiveNav"] = "attendance-roster";
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleWeekOff(
        int employeeId,
        string date,
        string isWeekOff,
        string week,
        int? teamId)
    {
        if (!HasAccess() || !CanManageTeam())
        {
            return Forbid();
        }

        if (!TryParseDay(date, out var day))
        {
            TempData["AttendanceError"] = "Invalid date on roster toggle.";
            return RedirectToAction(nameof(Index), new { week, teamId });
        }

        // HTML may post True/False/true/false/1/0
        var makeWeekOff = isWeekOff.Equals("true", StringComparison.OrdinalIgnoreCase)
                          || isWeekOff == "1"
                          || isWeekOff.Equals("on", StringComparison.OrdinalIgnoreCase);

        var actorId = AttendanceAccess.GetEmployeeId(User)!.Value;

        // Team leads may only edit their own team members
        if (!CanManageAll())
        {
            var ok = await attendance.CanViewerAccessEmployeeAsync(
                actorId,
                AttendanceAccess.GetAttendanceRole(User),
                employeeId);
            if (!ok)
            {
                return Forbid();
            }
        }

        try
        {
            var result = await attendance.SetRosterWeekOffAsync(employeeId, day, makeWeekOff, actorId);
            TempData[result.Ok ? "AttendanceSuccess" : "AttendanceError"] = result.Message;
        }
        catch (Exception ex)
        {
            TempData["AttendanceError"] =
                "Could not save roster. Check RosterEntries table exists. " + ex.Message;
        }

        var weekKey = TryParseDay(week, out var w) ? w.ToString("yyyy-MM-dd", Inv) : day.ToString("yyyy-MM-dd", Inv);
        return RedirectToAction(nameof(Index), new { week = weekKey, teamId });
    }
}
