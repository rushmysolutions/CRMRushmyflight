using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RushMyBookings.Crm.Helpers;
using RushMyBookings.Crm.Services;

namespace RushMyBookings.Crm.Controllers;

[Authorize]
[Route("api/push")]
public class PushController(IPushNotificationService push) : Controller
{
    /// <summary>Installed app: stretch the cookie so they stay signed in until Sign out.</summary>
    [HttpPost("keep-signed-in")]
    public async Task<IActionResult> KeepSignedIn()
    {
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            User,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddYears(1)
            });

        return Json(new { ok = true });
    }

    [HttpGet("public-key")]
    public IActionResult PublicKey()
    {
        if (!push.IsConfigured)
            return StatusCode(503, new { error = "Push is not set up yet." });

        return Json(new { publicKey = push.GetPublicKey() });
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var employeeId = AttendanceAccess.GetEmployeeId(User);
        var subscribed = employeeId is int id && await push.HasSubscriptionAsync(id);

        return Json(new
        {
            configured = push.IsConfigured,
            subscribed,
            isInternal = AttendanceAccess.IsInternalPortal(User),
            employeeId
        });
    }

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscribeBody body)
    {
        var employeeId = AttendanceAccess.GetEmployeeId(User);
        if (employeeId is null)
            return BadRequest(new { error = "Push reminders are only for Internal employees." });

        if (body == null ||
            string.IsNullOrWhiteSpace(body.Endpoint) ||
            body.Keys == null ||
            string.IsNullOrWhiteSpace(body.Keys.P256dh) ||
            string.IsNullOrWhiteSpace(body.Keys.Auth))
        {
            return BadRequest(new { error = "Bad subscription data." });
        }

        await push.SaveSubscriptionAsync(
            employeeId.Value,
            body.Endpoint,
            body.Keys.P256dh,
            body.Keys.Auth);

        return Json(new { ok = true });
    }

    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] PushSubscribeBody? body)
    {
        var employeeId = AttendanceAccess.GetEmployeeId(User);
        if (employeeId is null)
            return BadRequest(new { error = "Push reminders are only for Internal employees." });

        await push.RemoveSubscriptionAsync(employeeId.Value, body?.Endpoint);
        return Json(new { ok = true });
    }
}

public class PushSubscribeBody
{
    public string? Endpoint { get; set; }
    public PushKeysBody? Keys { get; set; }
}

public class PushKeysBody
{
    public string? P256dh { get; set; }
    public string? Auth { get; set; }
}
