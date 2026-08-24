namespace FamilyHub.Api.Notifications;

public class MedicationReminderWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<MedicationReminderWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(30);
    private static readonly Action<ILogger, Exception> LogDispatchFailure = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(1, "MedicationReminderDispatchFailed"),
        "Medication reminder dispatch failed.");

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
                var dispatcher = scope.ServiceProvider.GetRequiredService<MedicationReminderDispatcher>();
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