namespace RushMyBookings.Crm.Services;

/// <summary>
/// On/after the 1st of each month, locks the previous month's attendance for everyone.
/// Retries periodically so a missed run (app down on the 1st) still catches up.
/// </summary>
public sealed class AttendanceAutoLockHostedService(
    IServiceProvider services,
    ILogger<AttendanceAutoLockHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish starting before we touch the DB
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                var attendance = scope.ServiceProvider.GetRequiredService<IAttendanceService>();
                await attendance.EnsureDatabaseAsync();
                var result = await attendance.AutoLockPreviousMonthIfDueAsync();
                if (result.Ran)
                {
                    logger.LogInformation("Attendance auto-lock: {Message}", result.Message);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Attendance auto-lock check failed.");
            }

            try
            {
                // Check a few times a day — enough for catch-up without hammering SQL
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
