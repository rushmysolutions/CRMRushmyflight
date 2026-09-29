using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using RushMyBookings.Crm.Entities.Attendance;
using RushMyBookings.Crm.Helpers;
using RushMyBookings.Crm.Services;
using RushMyBookings.Crm.ViewModels;

namespace RushMyBookings.Crm.Controllers;

public class AccountController(ICrmDataService dataService, IAttendanceService attendanceService) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl, AttendanceAccess.IsInternalPortal(User));
        }

        return View(new LoginViewModel
        {
            ReturnUrl = returnUrl,
            Portal = LoginPortals.Crm
        });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Portal) ||
            !LoginPortals.All.Contains(model.Portal, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Portal), "Please select CRM or Internal.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var portal = model.Portal.Equals(LoginPortals.Internal, StringComparison.OrdinalIgnoreCase)
            ? LoginPortals.Internal
            : LoginPortals.Crm;

        if (portal == LoginPortals.Internal)
        {
            return await LoginInternalAsync(model);
        }

        return await LoginCrmAsync(model);
    }

    private async Task<IActionResult> LoginCrmAsync(LoginViewModel model)
    {
        try
        {
            var user = await dataService.ValidateUserAsync(model.Login, model.Password);
            if (user is null)
            {
                ModelState.AddModelError(string.Empty, "Invalid CRM username/email or password.");
                return View(model);
            }

            try
            {
                await dataService.GetDashboardStatsAsync();
            }
            catch
            {
                // Preload is best-effort so login is not blocked if stats fail.
            }

            await SignInCrmUserAsync(user, model.RememberMe);
            return RedirectToLocal(model.ReturnUrl, preferAttendance: false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException mysql)
        {
            return DatabaseError(model, mysql);
        }
        catch (InvalidOperationException ex) when (ex.InnerException is MySqlException mysql)
        {
            return DatabaseError(model, mysql);
        }
        catch (MySqlException ex)
        {
            return DatabaseError(model, ex);
        }
    }

    private async Task<IActionResult> LoginInternalAsync(LoginViewModel model)
    {
        try
        {
            var employee = await attendanceService.ValidateEmployeeLoginAsync(
                model.Login, model.Password);

            if (employee is null)
            {
                ModelState.AddModelError(string.Empty, "Invalid Internal username/email or password.");
                return View(model);
            }

            await SignInAttendanceEmployeeAsync(employee, model.RememberMe);
            return RedirectToLocal(model.ReturnUrl, preferAttendance: true);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty,
                "Cannot reach the Internal (SQL Server) attendance database. Check ConnectionStrings:AttendanceSqlServer. "
                + ex.Message);
            return View(model);
        }
    }

    private async Task SignInAttendanceEmployeeAsync(Entities.Attendance.Employee employee, bool rememberMe)
    {
        var claims = BuildAttendanceClaims(employee);
        claims.Add(new Claim(AttendanceClaimTypes.LoginPortal, LoginPortals.Internal));
        claims.Add(new Claim(ClaimTypes.Role, employee.RoleCode));
        claims.Add(new Claim(ClaimTypes.NameIdentifier, "att:" + employee.EmployeeId));
        claims.Add(new Claim(ClaimTypes.Name, employee.Username));
        claims.Add(new Claim(ClaimTypes.GivenName, employee.FirstName));
        claims.Add(new Claim(ClaimTypes.Surname, employee.LastName ?? string.Empty));
        claims.Add(new Claim(ClaimTypes.Email, employee.Email));

        await SignInWithClaimsAsync(claims, rememberMe);
    }

    private async Task SignInCrmUserAsync(Models.UserAccount user, bool rememberMe)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.Username ?? user.Email ?? user.UserId.ToString()),
            new(ClaimTypes.GivenName, user.FirstName ?? string.Empty),
            new(ClaimTypes.Surname, user.LastName ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Role, user.UserType ?? "General"),
            new(AttendanceClaimTypes.LoginPortal, LoginPortals.Crm)
        };

        await SignInWithClaimsAsync(claims, rememberMe);
    }

    private static List<Claim> BuildAttendanceClaims(Entities.Attendance.Employee employee)
    {
        var claims = new List<Claim>
        {
            new(AttendanceClaimTypes.EmployeeId, employee.EmployeeId.ToString()),
            new(AttendanceClaimTypes.AttendanceRole, employee.RoleCode)
        };

        if (employee.TeamId.HasValue)
        {
            claims.Add(new Claim(AttendanceClaimTypes.TeamId, employee.TeamId.Value.ToString()));
        }

        return claims;
    }

    private async Task SignInWithClaimsAsync(List<Claim> claims, bool rememberMe)
    {
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe
                    ? DateTimeOffset.UtcNow.AddDays(14)
                    : DateTimeOffset.UtcNow.AddHours(8)
            });
    }

    private IActionResult DatabaseError(LoginViewModel model, MySqlException ex)
    {
        var message = ex.Number switch
        {
            1045 => "Database login failed. Check the MySQL username and password in appsettings.json.",
            1049 => "Database not found on the server.",
            2003 or 2002 => "Cannot reach the MySQL server. Check the host, port 3306, and EC2 security group (allow your IP).",
            _ => $"Database connection error: {ex.Message}"
        };

        ModelState.AddModelError(string.Empty, message);
        return View(model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private IActionResult RedirectToLocal(string? returnUrl, bool preferAttendance = false)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        if (preferAttendance)
        {
            return RedirectToAction("Index", "Attendance");
        }

        return RedirectToAction("Index", "Home");
    }
}
