using System.Text.Json;
using WebPush;

namespace FamilyHub.Api.Notifications;

public class WebPushNotificationSender(IConfiguration configuration) : IPushNotificationSender
{
    public async Task SendAsync(
        BrowserPushSubscription subscription,
        PushNotification notification,
        CancellationToken cancellationToken)
    {
        var subject = configuration["Notifications:Vapid:Subject"]?.Trim();
        var publicKey = configuration["Notifications:Vapid:PublicKey"]?.Trim();
        var privateKey = configuration["Notifications:Vapid:PrivateKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(subject) ||
            string.IsNullOrWhiteSpace(publicKey) ||
            string.IsNullOrWhiteSpace(privateKey))
        {
            throw new InvalidOperationException("VAPID configuration is required to send Push notifications.");
        }

        var pushSubscription = new WebPush.PushSubscription(
            subscription.Endpoint,
            subscription.P256dh,
            subscription.Auth);
        var vapidDetails = new VapidDetails(subject, publicKey, privateKey);
        using var client = new WebPushClient();
        await client.SendNotificationAsync(
            pushSubscription,
            JsonSerializer.Serialize(notification, JsonSerializerOptions.Web),
            vapidDetails,
            cancellationToken);
    }
}