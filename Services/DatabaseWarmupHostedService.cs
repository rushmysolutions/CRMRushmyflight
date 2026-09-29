namespace RushMyBookings.Crm.Services;

public sealed class DatabaseWarmupHostedService(IServiceProvider services) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = services.CreateAsyncScope();
            var dataService = scope.ServiceProvider.GetRequiredService<ICrmDataService>();
            await dataService.CanConnectAsync();
        }
        catch
        {
            // Warmup is best-effort; the app still starts if the database is unreachable.
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
