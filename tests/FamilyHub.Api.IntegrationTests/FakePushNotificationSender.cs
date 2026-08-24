using FamilyHub.Api.Notifications;

namespace FamilyHub.Api.IntegrationTests;

public sealed class FakePushNotificationSender : IPushNotificationSender
{
    private readonly List<SentPushNotification> _notifications = [];

    public IReadOnlyList<SentPushNotification> Notifications => _notifications;

    public Task SendAsync(
        BrowserPushSubscription subscription,
        PushNotification notification,
        CancellationToken cancellationToken)
    {
        _notifications.Add(new SentPushNotification(subscription.Id, notification));
        return Task.CompletedTask;
    }

    public void Clear() => _notifications.Clear();
}

public record SentPushNotification(
    Guid PushSubscriptionId,
    PushNotification Notification);