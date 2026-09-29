using System.Text.RegularExpressions;
using RushMyBookings.Crm.Helpers;

namespace RushMyBookings.Crm.Middleware;

/// <summary>
/// Blocks phones and tablets for everyone except System Administrator.
/// Login/Logout stay open on mobile so a System Administrator can sign in.
/// </summary>
public partial class DeviceRestrictionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (AttendanceAccess.IsSystemAdministrator(context.User) || IsAccountAuthPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        var ua = context.Request.Headers.UserAgent.ToString();
        if (IsMobileOrTablet(ua))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(
                """
                <!DOCTYPE html>
                <html lang="en">
                <head>
                  <meta charset="utf-8" />
                  <meta name="viewport" content="width=device-width, initial-scale=1" />
                  <title>Desktop only</title>
                </head>
                <body style="font-family:system-ui,sans-serif;padding:2rem;max-width:28rem;margin:3rem auto;text-align:center;line-height:1.5;color:#1e293b;">
                  <h1 style="font-size:1.4rem;margin-bottom:0.75rem;">Desktop only</h1>
                  <p style="margin:0;color:#475569;">This application cannot be opened on a phone or tablet. Please use a desktop or laptop computer.</p>
                </body>
                </html>
                """);
            return;
        }

        await next(context);
    }

    private static bool IsAccountAuthPath(PathString path) =>
        path.StartsWithSegments("/Account/Login") ||
        path.StartsWithSegments("/Account/Logout");

    internal static bool IsMobileOrTablet(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return false;
        }

        return MobileOrTabletRegex().IsMatch(userAgent);
    }

    [GeneratedRegex(
        @"Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini|Mobile|Tablet|Kindle|Silk|PlayBook|Windows Phone",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MobileOrTabletRegex();
}

public static class DeviceRestrictionMiddlewareExtensions
{
    public static IApplicationBuilder UseDeviceRestriction(this IApplicationBuilder app) =>
        app.UseMiddleware<DeviceRestrictionMiddleware>();
}
