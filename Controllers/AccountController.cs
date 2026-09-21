using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using RushMyBookings.Crm.Services;
using RushMyBookings.Crm.ViewModels;

namespace RushMyBookings.Crm.Controllers;

public class AccountController(ICrmDataService dataService) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var user = await dataService.ValidateUserAsync(model.Login, model.Password, cancellationToken);
            if (user is null)
            {
                ModelState.AddModelError(string.Empty, "Invalid username/email or password.");
                return View(model);
            }

            try
            {
                await dataService.GetDashboardStatsAsync(cancellationToken);
            }
            catch
            {
                // Preload is best-effort so login is not blocked if stats fail.
            }

            await SignInUserAsync(user, model.RememberMe);
            return RedirectToLocal(model.ReturnUrl);
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

    private async Task SignInUserAsync(Models.UserAccount user, bool rememberMe)
    {

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.Username ?? user.Email ?? user.UserId.ToString()),
            new(ClaimTypes.GivenName, user.FirstName ?? string.Empty),
            new(ClaimTypes.Surname, user.LastName ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Role, user.UserType ?? "General")
        };

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

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }
}
