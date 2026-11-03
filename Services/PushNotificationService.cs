using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RushMyBookings.Crm.Data;
using RushMyBookings.Crm.Entities.Attendance;
using RushMyBookings.Crm.Helpers;
using RushMyBookings.Crm.ViewModels.Attendance;
using WebPush;

namespace RushMyBookings.Crm.Services;

public interface IPushNotificationService
{
    string? GetPublicKey();
    bool IsConfigured { get; }
    Task SaveSubscriptionAsync(int employeeId, string endpoint, string p256dh, string auth);
    Task RemoveSubscriptionAsync(int employeeId, string? endpoint);
    Task<bool> HasSubscriptionAsync(int employeeId);
    Task SendDueRemindersAsync(CancellationToken cancellationToken);
}

public class PushNotificationService(
    AttendanceDbContext db,
    IAttendanceService attendance,
    IOptions<VapidOptions> options,
    IWebHostEnvironment env,
    ILogger<PushNotificationService> logger) : IPushNotificationService
{
    private readonly VapidOptions _opts = options.Value;
    private VapidDetails? _vapid;
    private bool _loaded;

    public bool IsConfigured
    {
        get
        {
            LoadKeysIfNeeded();
            return _vapid != null;
        }
    }

    public string? GetPublicKey()
    {
        LoadKeysIfNeeded();
        return _vapid?.PublicKey;
    }

    public async Task SaveSubscriptionAsync(int employeeId, string endpoint, string p256dh, string auth)
    {
        if (employeeId <= 0 ||
            string.IsNullOrWhiteSpace(endpoint) ||
            string.IsNullOrWhiteSpace(p256dh) ||
            string.IsNullOrWhiteSpace(auth))
        {
            throw new ArgumentException("Invalid push subscription.");
        }

        var row = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint);
        if (row == null)
        {
            db.PushSubscriptions.Add(new PushSubscriptionRecord
            {
                EmployeeId = employeeId,
                Endpoint = endpoint.Trim(),
                P256dh = p256dh.Trim(),
                Auth = auth.Trim(),
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            row.EmployeeId = employeeId;
            row.P256dh = p256dh.Trim();
            row.Auth = auth.Trim();
            row.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    public async Task RemoveSubscriptionAsync(int employeeId, string? endpoint)
    {
        var q = db.PushSubscriptions.Where(s => s.EmployeeId == employeeId);
        if (!string.IsNullOrWhiteSpace(endpoint))
            q = q.Where(s => s.Endpoint == endpoint);

        var rows = await q.ToListAsync();
        if (rows.Count == 0)
            return;

        db.PushSubscriptions.RemoveRange(rows);
        await db.SaveChangesAsync();
    }

    public Task<bool> HasSubscriptionAsync(int employeeId) =>
        db.PushSubscriptions.AsNoTracking().AnyAsync(s => s.EmployeeId == employeeId);

    public async Task SendDueRemindersAsync(CancellationToken cancellationToken)
    {
        if (!_opts.Enabled || !IsConfigured)
            return;

        var minutesBefore = _opts.MinutesBeforeShift;
        if (minutesBefore < 0) minutesBefore = 0;
        if (minutesBefore > 120) minutesBefore = 120;

        var lead = TimeSpan.FromMinutes(minutesBefore);
        var now = DateTime.Now;

        var subs = await db.PushSubscriptions.AsNoTracking().ToListAsync(cancellationToken);
        if (subs.Count == 0)
            return;

        foreach (var group in subs.GroupBy(s => s.EmployeeId))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var employeeId = group.Key;
            ResolvedWorkDay work;
            try
            {
                work = await attendance.ResolveWorkDayAsync(employeeId, now);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not resolve work day for employee {EmployeeId}", employeeId);
                continue;
            }

            await MaybeSendLoginReminderAsync(group, employeeId, work, now, lead, cancellationToken);
            await MaybeSendLogoutReminderAsync(group, employeeId, work, now, cancellationToken);
        }
    }

    private async Task MaybeSendLoginReminderAsync(
        IGrouping<int, PushSubscriptionRecord> group,
        int employeeId,
        ResolvedWorkDay work,
        DateTime now,
        TimeSpan lead,
        CancellationToken ct)
    {
        if (!work.HasShift || work.IsWeekOff || work.ShiftStart == null)
            return;

        var shiftStart = work.AttendanceDate.ToDateTime(TimeOnly.FromTimeSpan(work.ShiftStart.Value));
        // From a few minutes before start → 1 hour after (once per day)
        if (now < shiftStart - lead || now > shiftStart.AddHours(1))
            return;

        var alreadyIn = await db.DailyAttendances.AsNoTracking().AnyAsync(a =>
            a.EmployeeId == employeeId &&
            a.AttendanceDate == work.AttendanceDate &&
            a.OptInTime != null, ct);

        if (alreadyIn)
            return;

        var shiftLabel = work.ShiftName ?? "shift";
        var body = work.ShiftStart is TimeSpan start && work.ShiftEnd is TimeSpan end
            ? $"{shiftLabel} ({AttendanceClock.FormatShift(start, end)}) is starting. Open attendance to opt in."
            : $"{shiftLabel} is starting. Open attendance to opt in.";

        var payload = JsonSerializer.Serialize(new
        {
            title = "Time to log in",
            body,
            url = "/Attendance",
            tag = $"login-{work.AttendanceDate:yyyyMMdd}-{employeeId}"
        });

        foreach (var sub in group)
        {
            if (sub.LastLoginReminderFor == work.AttendanceDate)
                continue;

            if (!await TrySendAsync(sub, payload, ct))
                continue;

            await MarkReminderAsync(sub.PushSubscriptionId, loginFor: work.AttendanceDate, logoutFor: null, ct);
        }
    }

    private async Task MaybeSendLogoutReminderAsync(
        IGrouping<int, PushSubscriptionRecord> group,
        int employeeId,
        ResolvedWorkDay work,
        DateTime now,
        CancellationToken ct)
    {
        // Find an open day (opted in, not out) — today first, else yesterday still open
        var day = await db.DailyAttendances.AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.EmployeeId == employeeId &&
                a.AttendanceDate == work.AttendanceDate &&
                a.OptInTime != null &&
                a.OptOutTime == null, ct);

        if (day == null)
        {
            var yesterday = work.AttendanceDate.AddDays(-1);
            day = await db.DailyAttendances.AsNoTracking()
                .FirstOrDefaultAsync(a =>
                    a.EmployeeId == employeeId &&
                    a.AttendanceDate == yesterday &&
                    a.OptInTime != null &&
                    a.OptOutTime == null, ct);
        }

        if (day?.OptInTime == null)
            return;

        // Use that open day's schedule (night shift / late logout)
        ResolvedWorkDay dayWork;
        try
        {
            var probe = day.AttendanceDate.ToDateTime(TimeOnly.FromTimeSpan(day.OptInTime.Value)).AddMinutes(1);
            dayWork = await attendance.ResolveWorkDayAsync(employeeId, probe);
        }
        catch
        {
            dayWork = work;
        }

        DateTime due;
        string body;
        var minutesBefore = Math.Clamp(_opts.MinutesBeforeShift, 0, 120);

        if (dayWork.HasShift && dayWork.ShiftEnd != null && dayWork.ShiftStart != null)
        {
            // Fixed shift → a few minutes before shift end
            due = ShiftEndDateTime(day.AttendanceDate, dayWork.ShiftStart.Value, dayWork.ShiftEnd.Value)
                .AddMinutes(-minutesBefore);
            var timing = AttendanceClock.FormatShift(dayWork.ShiftStart.Value, dayWork.ShiftEnd.Value);
            body = $"Your shift ({dayWork.ShiftName ?? "shift"}: {timing}) is ending. Opt out / sign out when you're done.";
        }
        else
        {
            // Flexible / no shift → ~9 hours after opt-in
            due = PunchDateTime(day.AttendanceDate, day.OptInTime.Value, dayWork.ShiftStart, dayWork.ShiftEnd)
                .AddHours(9)
                .AddMinutes(-minutesBefore);
            body = "You've been opted in for about 9 hours. Opt out / sign out if you're done for the day.";
        }

        // From due → 3 hours later (once per day via LastLogoutReminderFor)
        if (now < due || now > due.AddHours(3))
            return;

        var payload = JsonSerializer.Serialize(new
        {
            title = "Time to log out",
            body,
            url = "/Attendance",
            tag = $"logout-{day.AttendanceDate:yyyyMMdd}-{employeeId}"
        });

        foreach (var sub in group)
        {
            if (sub.LastLogoutReminderFor == day.AttendanceDate)
                continue;

            if (!await TrySendAsync(sub, payload, ct))
                continue;

            await MarkReminderAsync(sub.PushSubscriptionId, loginFor: null, logoutFor: day.AttendanceDate, ct);
        }
    }

    private async Task MarkReminderAsync(
        int subscriptionId,
        DateOnly? loginFor,
        DateOnly? logoutFor,
        CancellationToken ct)
    {
        var tracked = await db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.PushSubscriptionId == subscriptionId, ct);
        if (tracked == null)
            return;

        if (loginFor.HasValue)
            tracked.LastLoginReminderFor = loginFor;
        if (logoutFor.HasValue)
            tracked.LastLogoutReminderFor = logoutFor;

        tracked.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static DateTime ShiftEndDateTime(DateOnly attendanceDate, TimeSpan shiftStart, TimeSpan shiftEnd)
    {
        if (shiftEnd < shiftStart)
            return attendanceDate.AddDays(1).ToDateTime(TimeOnly.FromTimeSpan(shiftEnd));

        return attendanceDate.ToDateTime(TimeOnly.FromTimeSpan(shiftEnd));
    }

    // Opt-in after midnight on a night shift still belongs to the start day.
    private static DateTime PunchDateTime(
        DateOnly attendanceDate,
        TimeSpan punch,
        TimeSpan? shiftStart,
        TimeSpan? shiftEnd)
    {
        var overnight = shiftStart.HasValue && shiftEnd.HasValue && shiftEnd < shiftStart;
        if (overnight && punch < shiftEnd!.Value)
            return attendanceDate.AddDays(1).ToDateTime(TimeOnly.FromTimeSpan(punch));

        return attendanceDate.ToDateTime(TimeOnly.FromTimeSpan(punch));
    }

    private async Task<bool> TrySendAsync(PushSubscriptionRecord sub, string payload, CancellationToken ct)
    {
        LoadKeysIfNeeded();
        if (_vapid == null)
            return false;

        try
        {
            var pushSub = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
            using var client = new WebPushClient();
            await client.SendNotificationAsync(pushSub, payload, _vapid, ct);
            return true;
        }
        catch (WebPushException ex) when ((int)ex.StatusCode is 404 or 410)
        {
            logger.LogInformation("Dropping expired push subscription {Id}", sub.PushSubscriptionId);
            var dead = await db.PushSubscriptions
                .FirstOrDefaultAsync(s => s.PushSubscriptionId == sub.PushSubscriptionId, ct);
            if (dead != null)
            {
                db.PushSubscriptions.Remove(dead);
                await db.SaveChangesAsync(ct);
            }
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Push failed for subscription {Id}", sub.PushSubscriptionId);
            return false;
        }
    }

    private void LoadKeysIfNeeded()
    {
        if (_loaded)
            return;
        _loaded = true;

        var subject = string.IsNullOrWhiteSpace(_opts.Subject)
            ? "mailto:admin@rushmybookings.local"
            : _opts.Subject.Trim();

        var publicKey = (_opts.PublicKey ?? "").Trim();
        var privateKey = (_opts.PrivateKey ?? "").Trim();
        var file = Path.Combine(env.ContentRootPath, "App_Data", "vapid.json");

        if (string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(privateKey))
        {
            if (File.Exists(file))
            {
                try
                {
                    var stored = JsonSerializer.Deserialize<StoredVapid>(File.ReadAllText(file));
                    if (stored != null &&
                        !string.IsNullOrWhiteSpace(stored.PublicKey) &&
                        !string.IsNullOrWhiteSpace(stored.PrivateKey))
                    {
                        publicKey = stored.PublicKey;
                        privateKey = stored.PrivateKey;
                        if (!string.IsNullOrWhiteSpace(stored.Subject))
                            subject = stored.Subject;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not read App_Data/vapid.json");
                }
            }
        }

        if (string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(privateKey))
        {
            var generated = VapidHelper.GenerateVapidKeys();
            publicKey = generated.PublicKey;
            privateKey = generated.PrivateKey;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllText(file, JsonSerializer.Serialize(new StoredVapid
                {
                    Subject = subject,
                    PublicKey = publicKey,
                    PrivateKey = privateKey
                }, new JsonSerializerOptions { WriteIndented = true }));
                logger.LogInformation("Created Web Push keys in App_Data/vapid.json");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not write App_Data/vapid.json");
            }
        }

        if (!string.IsNullOrEmpty(publicKey) && !string.IsNullOrEmpty(privateKey))
            _vapid = new VapidDetails(subject, publicKey, privateKey);
        else
            logger.LogWarning("Web Push keys are missing.");
    }

    private class StoredVapid
    {
        public string Subject { get; set; } = "";
        public string PublicKey { get; set; } = "";
        public string PrivateKey { get; set; } = "";
    }
}
