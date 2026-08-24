using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace FamilyHub.Services;

public class PushNotificationClient(HttpClient http, IJSRuntime js)
{
    public async Task<string> GetPermissionStatusAsync() =>
        await js.InvokeAsync<string>("familyHubPushNotifications.getPermissionStatus");

    public async Task<PushSubscriptionRegistration?> RegisterAsync(bool requestPermission)
    {
        var key = await http.GetFromJsonAsync<VapidPublicKeyResponse>(
            "/api/push-subscriptions/vapid-public-key");
        if (key is null)
        {
            return null;
        }

        var browserSubscription = await js.InvokeAsync<BrowserPushSubscription>(
            "familyHubPushNotifications.subscribe",
            key.PublicKey,
            requestPermission);
        if (browserSubscription.Status != "granted" || browserSubscription.Endpoint is null)
        {
            return new PushSubscriptionRegistration(null, browserSubscription.Status);
        }

        var response = await http.PostAsJsonAsync("/api/push-subscriptions", new
        {
            endpoint = browserSubscription.Endpoint,
            p256dh = browserSubscription.P256dh,
            auth = browserSubscription.Auth,
            timeZoneId = browserSubscription.TimeZoneId,
        });
        response.EnsureSuccessStatusCode();
        var registered = await response.Content.ReadFromJsonAsync<PushSubscriptionResponse>();
        return new PushSubscriptionRegistration(registered!.PushSubscriptionId, "granted");
    }

    public async Task SendTestAsync(Guid pushSubscriptionId)
    {
        var response = await http.PostAsync(
            $"/api/push-subscriptions/{pushSubscriptionId}/test",
            null);
        response.EnsureSuccessStatusCode();
    }
}

public record PushSubscriptionRegistration(Guid? PushSubscriptionId, string Status);

internal sealed record VapidPublicKeyResponse(string PublicKey);

internal sealed record PushSubscriptionResponse(Guid PushSubscriptionId);

internal sealed record BrowserPushSubscription(
    string Status,
    string? Endpoint,
    string? P256dh,
    string? Auth,
    string? TimeZoneId);