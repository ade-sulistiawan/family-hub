namespace FamilyHub.Api.Notifications;

public class LowStockReminderWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<LowStockReminderWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(5);
    private static readonly Action<ILogger, Exception> LogDispatchFailure = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(1, "LowStockReminderDispatchFailed"),
        "Low-stock reminder dispatch failed.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Notifications:PollingEnabled", true))
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<LowStockReminderDispatcher>();
                await dispatcher.DispatchDueAsync(timeProvider.GetUtcNow(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogDispatchFailure(logger, exception);
            }

            await Task.Delay(PollingInterval, timeProvider, stoppingToken);
        }
    }
}
