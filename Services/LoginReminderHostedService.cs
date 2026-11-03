namespace RushMyBookings.Crm.Services;

/// <summary>
/// Every minute: login reminders at shift start, logout reminders at shift end / after 9 hours.
/// Requires dbo.PushSubscriptions (see database/PushSubscriptions.sql).
/// </summary>
public class LoginReminderHostedService(
    IServiceProvider services,
    ILogger<LoginReminderHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish starting before we touch the DB
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(25), stoppingToken);
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
                var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
                await push.SendDueRemindersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Attendance push reminder check failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
