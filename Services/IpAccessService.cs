using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RushMyBookings.Crm.Data;
using RushMyBookings.Crm.Entities.Attendance;
using RushMyBookings.Crm.ViewModels.Attendance;

namespace RushMyBookings.Crm.Services;

public interface IIpAccessService
{
    Task<IReadOnlyList<AllowedIpListItem>> GetAllowedIpsAsync();

    Task<(bool Ok, string Message)> AddAsync(string ipAddress, string? note, int? createdByEmployeeId);

    Task<(bool Ok, string Message)> DeleteAsync(int allowedIpId);

    /// <summary>
    /// Empty allowlist = open to everyone.
    /// Once any IP is saved, only listed IPs (plus localhost) can open the app.
    /// </summary>
    Task<bool> IsClientAllowedAsync(string? clientIp);
}

public class IpAccessService(AttendanceDbContext db, IMemoryCache cache) : IIpAccessService
{
    private const string CacheKey = "attendance:allowed-ips";
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(2);

    public async Task<IReadOnlyList<AllowedIpListItem>> GetAllowedIpsAsync()
    {
        return await db.AllowedIpAddresses.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.IpAddress)
            .Select(x => new AllowedIpListItem
            {
                AllowedIpId = x.AllowedIpId,
                IpAddress = x.IpAddress,
                Note = x.Note,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<(bool Ok, string Message)> AddAsync(
        string ipAddress,
        string? note,
        int? createdByEmployeeId)
    {
        var normalized = NormalizeIp(ipAddress);
        if (normalized is null)
        {
            return (false, "Enter a valid IPv4 or IPv6 address.");
        }

        if (await db.AllowedIpAddresses.AnyAsync(x => x.IpAddress == normalized))
        {
            return (false, "This IP address is already in the list.");
        }

        db.AllowedIpAddresses.Add(new AllowedIpAddress
        {
            IpAddress = normalized,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedByEmployeeId = createdByEmployeeId
        });

        await db.SaveChangesAsync();
        cache.Remove(CacheKey);
        return (true, $"IP {normalized} allowed.");
    }

    public async Task<(bool Ok, string Message)> DeleteAsync(int allowedIpId)
    {
        var row = await db.AllowedIpAddresses.FirstOrDefaultAsync(x => x.AllowedIpId == allowedIpId);
        if (row is null)
        {
            return (false, "IP address not found.");
        }

        db.AllowedIpAddresses.Remove(row);
        await db.SaveChangesAsync();
        cache.Remove(CacheKey);
        return (true, $"IP {row.IpAddress} removed.");
    }

    public async Task<bool> IsClientAllowedAsync(string? clientIp)
    {
        var allowed = await GetCachedAllowedIpsAsync();

        // Nothing configured yet → don't lock anyone out
        if (allowed.Count == 0)
        {
            return true;
        }

        var normalized = NormalizeIp(clientIp);
        if (normalized is null)
        {
            return false;
        }

        // Always keep localhost so you can recover from the server machine
        if (IPAddress.TryParse(normalized, out var parsed) && IPAddress.IsLoopback(parsed))
        {
            return true;
        }

        return allowed.Contains(normalized);
    }

    private async Task<HashSet<string>> GetCachedAllowedIpsAsync()
    {
        if (cache.TryGetValue(CacheKey, out HashSet<string>? cached) && cached is not null)
        {
            return cached;
        }

        var list = await db.AllowedIpAddresses.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => x.IpAddress)
            .ToListAsync();

        var set = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
        cache.Set(CacheKey, set, CacheFor);
        return set;
    }

    /// <summary>Normalizes to a comparable string (maps ::ffff:x.x.x.x → x.x.x.x).</summary>
    public static string? NormalizeIp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var raw = value.Trim();
        // X-Forwarded-For may be "client, proxy"
        if (raw.Contains(','))
        {
            raw = raw.Split(',')[0].Trim();
        }

        if (!IPAddress.TryParse(raw, out var ip))
        {
            return null;
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        return ip.ToString();
    }
}
