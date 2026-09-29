using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RushMyBookings.Crm.Entities.Attendance;
using RushMyBookings.Crm.Helpers;
using RushMyBookings.Crm.Services;
using RushMyBookings.Crm.ViewModels.Attendance;

namespace RushMyBookings.Crm.Controllers;

[Authorize]
public class EmployeesController(IAttendanceService attendance) : Controller
{
    private bool EnsureManager() =>
        AttendanceAccess.CanManageEmployees(User) && AttendanceAccess.HasAttendanceAccess(User);

    private async Task BindLookupsAsync(EmployeeEditViewModel model)
    {
        model.Teams = await attendance.GetTeamsAsync();
        model.Leads = await attendance.GetLeadOptionsAsync();
        model.Designations = await attendance.GetDesignationsAsync();
        model.Departments = await attendance.GetDepartmentsAsync();
        model.Roles = await attendance.GetRolesAsync();
        if (model.RoleId == 0 && model.Roles.Count > 0)
        {
            model.RoleId = model.Roles
                .FirstOrDefault(r => r.Code == AttendanceRoles.Employee)?.RoleId
                ?? model.Roles[0].RoleId;
        }
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q)
    {
        if (!EnsureManager())
        {
            return Forbid();
        }

        ViewData["Title"] = "Employees";
        ViewData["ActiveNav"] = "attendance-employees";
        ViewBag.Search = q;
        return View(await attendance.GetEmployeesAsync(q));
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!EnsureManager())
        {
            return Forbid();
        }

        ViewData["Title"] = "Add Employee";
        ViewData["ActiveNav"] = "attendance-employees";

        var model = new EmployeeEditViewModel();
        await BindLookupsAsync(model);
        return View("Edit", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EmployeeEditViewModel model)
    {
        if (!EnsureManager())
        {
            return Forbid();
        }

        await BindLookupsAsync(model);

        // EmpCode is auto-generated from department on create
        ModelState.Remove(nameof(model.EmpCode));

        if (string.IsNullOrWhiteSpace(model.Department))
        {
            ModelState.AddModelError(nameof(model.Department), "Department is required.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Add Employee";
            ViewData["ActiveNav"] = "attendance-employees";
            return View("Edit", model);
        }

        var result = await attendance.CreateEmployeeAsync(model, AttendanceAccess.GetEmployeeId(User));
        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            ViewData["Title"] = "Add Employee";
            ViewData["ActiveNav"] = "attendance-employees";
            return View("Edit", model);
        }

        TempData["AttendanceSuccess"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> NextEmpCode(string? department)
    {
        if (!EnsureManager())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(department))
        {
            return Json(new { code = "" });
        }

        var code = await attendance.GenerateNextEmpCodeAsync(department);
        return Json(new { code = code ?? "" });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (!EnsureManager())
        {
            return Forbid();
        }

        var emp = await attendance.GetEmployeeAsync(id);
        if (emp is null)
        {
            return NotFound();
        }

        ViewData["Title"] = "Edit Employee";
        ViewData["ActiveNav"] = "attendance-employees";

        var model = new EmployeeEditViewModel
        {
            EmployeeId = emp.EmployeeId,
            EmpCode = emp.EmpCode,
            FirstName = emp.FirstName,
            LastName = emp.LastName,
            Email = emp.Email,
            Phone = emp.Phone,
            Department = emp.Department,
            Designation = emp.Designation,
            Username = emp.Username,
            RoleId = emp.RoleId,
            TeamId = emp.TeamId,
            ReportingLeadId = emp.ReportingLeadId,
            CrmUserId = emp.CrmUserId,
            JoinDate = emp.JoinDate,
            IsActive = emp.IsActive
        };
        await BindLookupsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EmployeeEditViewModel model)
    {
        if (!EnsureManager())
        {
            return Forbid();
        }

        await BindLookupsAsync(model);

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Edit Employee";
            ViewData["ActiveNav"] = "attendance-employees";
            return View(model);
        }

        var result = await attendance.UpdateEmployeeAsync(model);
        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            ViewData["Title"] = "Edit Employee";
            ViewData["ActiveNav"] = "attendance-employees";
            return View(model);
        }

        TempData["AttendanceSuccess"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Teams()
    {
        if (!EnsureManager())
        {
            return Forbid();
        }

        ViewData["Title"] = "Teams";
        ViewData["ActiveNav"] = "attendance-teams";
        ViewBag.Leads = await attendance.GetLeadOptionsAsync();
        return View(await attendance.GetTeamsAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTeam(string name, int? leadEmployeeId)
    {
        if (!EnsureManager())
        {
            return Forbid();
        }

        var result = await attendance.CreateTeamAsync(name, leadEmployeeId);
        TempData[result.Ok ? "AttendanceSuccess" : "AttendanceError"] = result.Message;
        return RedirectToAction(nameof(Teams));
    }

    [HttpGet]
    public async Task<IActionResult> Designations()
    {
        if (!EnsureManager())
        {
            return Forbid();
        }

        ViewData["Title"] = "Designations";
        ViewData["ActiveNav"] = "attendance-designations";
        return View(await attendance.GetDesignationsAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDesignation(string name)
    {
        if (!EnsureManager())
        {
            return Forbid();
        }

        var result = await attendance.CreateDesignationAsync(name);
        TempData[result.Ok ? "AttendanceSuccess" : "AttendanceError"] = result.Message;
        return RedirectToAction(nameof(Designations));
    }

    [HttpGet]
    public async Task<IActionResult> Departments()
    {
        if (!EnsureManager())
        {
            return Forbid();
        }

        ViewData["Title"] = "Departments";
        ViewData["ActiveNav"] = "attendance-departments";
        return View(await attendance.GetDepartmentsAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDepartment(string name)
    {
        if (!EnsureManager())
        {
            return Forbid();
        }

        var result = await attendance.CreateDepartmentAsync(name);
        TempData[result.Ok ? "AttendanceSuccess" : "AttendanceError"] = result.Message;
        return RedirectToAction(nameof(Departments));
    }

    [HttpGet]
    public async Task<IActionResult> Holidays(int? year)
    {
        if (!EnsureManager())
        {
            return Forbid();
        }

        var y = year ?? DateTime.Today.Year;
        ViewData["Title"] = "Holidays";
        ViewData["ActiveNav"] = "attendance-holidays";
        ViewBag.Year = y;
        return View(await attendance.GetHolidaysAsync(y));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateHoliday(
        DateOnly holidayDate,
        string name,
        bool isOptional)
    {
        if (!EnsureManager())
        {
            return Forbid();
        }

        var result = await attendance.CreateHolidayAsync(holidayDate, name, isOptional);
        TempData[result.Ok ? "AttendanceSuccess" : "AttendanceError"] = result.Message;
        return RedirectToAction(nameof(Holidays), new { year = holidayDate.Year });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteHoliday(int holidayId, int year)
    {
        if (!EnsureManager())
        {
            return Forbid();
        }

        var result = await attendance.DeleteHolidayAsync(holidayId);
        TempData[result.Ok ? "AttendanceSuccess" : "AttendanceError"] = result.Message;
        return RedirectToAction(nameof(Holidays), new { year });
    }
}
