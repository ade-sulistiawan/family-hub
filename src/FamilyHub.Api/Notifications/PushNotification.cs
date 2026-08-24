namespace FamilyHub.Api.Notifications;

public record PushNotification(string Title, string Body, string Url, string Tag);

public interface IPushNotificationSender
{
    Task SendAsync(
        BrowserPushSubscription subscription,
        PushNotification notification,
        CancellationToken cancellationToken);
}