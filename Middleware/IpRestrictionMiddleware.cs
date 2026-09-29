using System.Net;
using RushMyBookings.Crm.Helpers;
using RushMyBookings.Crm.Services;

namespace RushMyBookings.Crm.Middleware;

/// <summary>
/// IP allow-list for everyone except System Administrator.
/// Login/Logout stay reachable from any IP so a System Administrator can sign in.
/// </summary>
public class IpRestrictionMiddleware(RequestDelegate next, ILogger<IpRestrictionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IIpAccessService ipAccess)
    {
        if (AttendanceAccess.IsSystemAdministrator(context.User) || IsAccountAuthPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        var clientIp = ResolveClientIp(context);
        bool allowed;
        try
        {
            allowed = await ipAccess.IsClientAllowedAsync(clientIp);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "IP allow-list check failed; allowing request.");
            allowed = true;
        }

        if (!allowed)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(
                "<!DOCTYPE html><html><head><title>Access denied</title></head>" +
                "<body style=\"font-family:system-ui,sans-serif;padding:2rem;max-width:40rem;\">" +
                "<h1>Access denied</h1>" +
                "<p>This application is only available from approved IP addresses.</p>" +
                $"<p>Your IP: <code>{System.Net.WebUtility.HtmlEncode(clientIp ?? "unknown")}</code></p>" +
                "</body></html>");
            return;
        }

        await next(context);
    }

    private static bool IsAccountAuthPath(PathString path) =>
        path.StartsWithSegments("/Account/Login") ||
        path.StartsWithSegments("/Account/Logout");

    internal static string? ResolveClientIp(HttpContext context)
    {
        var remote = context.Connection.RemoteIpAddress;
        if (remote is not null)
        {
            if (remote.IsIPv4MappedToIPv6)
            {
                remote = remote.MapToIPv4();
            }

            if (!IPAddress.IsLoopback(remote) ||
                string.IsNullOrEmpty(context.Request.Headers["X-Forwarded-For"]))
            {
                return IpAccessService.NormalizeIp(remote.ToString());
            }
        }

        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        return IpAccessService.NormalizeIp(forwarded);
    }
}

public static class IpRestrictionMiddlewareExtensions
{
    public static IApplicationBuilder UseIpRestriction(this IApplicationBuilder app) =>
        app.UseMiddleware<IpRestrictionMiddleware>();
}
