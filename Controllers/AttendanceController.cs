using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RushMyBookings.Crm.Entities.Attendance;
using RushMyBookings.Crm.Helpers;
using RushMyBookings.Crm.Services;
using RushMyBookings.Crm.ViewModels.Attendance;

namespace RushMyBookings.Crm.Controllers;

[Authorize]
public class AttendanceController(IAttendanceService attendance) : Controller
{
    private IActionResult? RequireAttendanceEmployee()
    {
        if (!AttendanceAccess.HasAttendanceAccess(User))
        {
            return View("NoAccess");
        }

        return null;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? year, int? month, string? date)
    {
        var denied = RequireAttendanceEmployee();
        if (denied is not null)
        {
            return denied;
        }

        var employeeId = AttendanceAccess.GetEmployeeId(User)!.Value;
        var employee = await attendance.GetEmployeeAsync(employeeId);
        if (employee is null)
        {
            return View("NoAccess");
        }

        var (y, m) = AttendancePeriodHelper.Clamp(year, month);
        var work = await attendance.ResolveWorkDayAsync(employeeId);
        var today = work.AttendanceDate;
        ViewBag.ShiftLabel = work.HasShift
            ? $"{work.ShiftName} {AttendanceClock.FormatShift(work.ShiftStart!.Value, work.ShiftEnd!.Value)}"
            : null;
        ViewBag.IsWeekOff = work.IsWeekOff;

        DateOnly selectedDate = today;
        if (!string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var parsed))
        {
            selectedDate = parsed;
            // Keep calendar on the selected date's month when valid
            (y, m) = AttendancePeriodHelper.Clamp(parsed.Year, parsed.Month);
        }
        else if (y == today.Year && m == today.Month)
        {
            selectedDate = today;
        }
        else
        {
            // Default to last day of selected month that is not in the future
            var last = new DateOnly(y, m, DateTime.DaysInMonth(y, m));
            selectedDate = last > today ? today : last;
        }

        var monthView = await attendance.GetMonthViewAsync(employeeId, y, m);
        var monthLocked = await attendance.IsMonthLockedAsync(employeeId, y, m);
        monthView.CanEdit = monthView.CanEdit && !monthLocked;

        var day = monthView.Days.FirstOrDefault(d => d.Date == selectedDate);
        // Only default to Present for today (opt-in dropdown). Past unmarked days stay blank.
        var status = day?.Status;
        if (string.IsNullOrEmpty(status) && selectedDate == today)
        {
            status = DailyAttendanceStatus.Present;
        }

        var mark = new MarkAttendanceViewModel
        {
            EmployeeId = employeeId,
            EmployeeName = employee.FullName,
            AttendanceDate = selectedDate,
            OptInTime = day?.OptIn,
            OptOutTime = day?.OptOut,
            HasOptedIn = day?.HasOptedIn == true,
            HasOptedOut = day?.HasOptedOut == true,
            Status = status ?? string.Empty,
            Remarks = day?.Remarks,
            MonthLocked = monthLocked
        };

        ViewData["Title"] = "My Attendance";
        ViewData["ActiveNav"] = "attendance-mine";
        ViewBag.AllowedMonths = AttendancePeriodHelper.GetAllowedMonths();
        ViewBag.Today = today;

        return View(new MyAttendanceHomeViewModel { Today = mark, CurrentMonth = monthView });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleOptInOut(string? status)
    {
        var denied = RequireAttendanceEmployee();
        if (denied is not null)
        {
            return denied;
        }

        var employeeId = AttendanceAccess.GetEmployeeId(User)!.Value;
        var result = await attendance.ToggleOptInOutAsync(employeeId, status);
        TempData[result.Ok ? "AttendanceSuccess" : "AttendanceError"] = result.Message;

        var today = DateOnly.FromDateTime(DateTime.Today);
        return RedirectToAction(nameof(Index), new
        {
            year = today.Year,
            month = today.Month,
            date = today.ToString("yyyy-MM-dd")
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkOwnStatus(string status)
    {
        var denied = RequireAttendanceEmployee();
        if (denied is not null)
        {
            return denied;
        }

        var employeeId = AttendanceAccess.GetEmployeeId(User)!.Value;
        var result = await attendance.MarkOwnStatusAsync(employeeId, status);
        TempData[result.Ok ? "AttendanceSuccess" : "AttendanceError"] = result.Message;

        var today = DateOnly.FromDateTime(DateTime.Today);
        return RedirectToAction(nameof(Index), new
        {
            year = today.Year,
            month = today.Month,
            date = today.ToString("yyyy-MM-dd")
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Mark(MarkAttendanceViewModel model)
    {
        var denied = RequireAttendanceEmployee();
        if (denied is not null)
        {
            return denied;
        }

        var actorId = AttendanceAccess.GetEmployeeId(User)!.Value;
        var role = AttendanceAccess.GetAttendanceRole(User);
        var isManager = role is AttendanceRoles.SystemAdministrator
            or AttendanceRoles.Admin
            or AttendanceRoles.HR;

        if (!isManager)
        {
            return Forbid();
        }

        var allowed = await attendance.CanViewerAccessEmployeeAsync(actorId, role, model.EmployeeId);
        if (!allowed && role is not (AttendanceRoles.SystemAdministrator or AttendanceRoles.Admin or AttendanceRoles.HR))
        {
            return Forbid();
        }

        var result = await attendance.MarkAttendanceAsync(model, actorId, isSelf: false);
        TempData[result.Ok ? "AttendanceSuccess" : "AttendanceError"] = result.Message;

        return RedirectToAction(nameof(Month), new { id = model.EmployeeId, year = model.AttendanceDate.Year, month = model.AttendanceDate.Month });
    }

    [HttpGet]
    public async Task<IActionResult> Month(int? id, int? year, int? month)
    {
        var denied = RequireAttendanceEmployee();
        if (denied is not null)
        {
            return denied;
        }

        var viewerId = AttendanceAccess.GetEmployeeId(User)!.Value;
        var role = AttendanceAccess.GetAttendanceRole(User);
        var targetId = id ?? viewerId;
        var (y, m) = AttendancePeriodHelper.Clamp(year, month);

        if (!await attendance.CanViewerAccessEmployeeAsync(viewerId, role, targetId))
        {
            return Forbid();
        }

        var vm = await attendance.GetMonthViewAsync(targetId, y, m);
        vm.CanEdit = vm.CanEdit && (targetId == viewerId || AttendanceAccess.CanManageEmployees(User));
        ViewBag.AllowedMonths = AttendancePeriodHelper.GetAllowedMonths();

        ViewData["Title"] = "Monthly Attendance";
        ViewData["ActiveNav"] = targetId == viewerId ? "attendance-mine" : "attendance-reports";
        ViewBag.CanManageLock = AttendanceAccess.CanCloseMonth(User);
        ViewBag.CanHrMark = AttendanceAccess.CanManageEmployees(User);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetMonthLock(
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
        return RedirectToAction(nameof(Month), new { id = employeeId, year, month });
    }
}
