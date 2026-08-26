using System.Net.Http.Json;
using FamilyHub.Api.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyHub.Api.IntegrationTests;

public class LowStockNotificationTests : IClassFixture<FamilyHubApiFactory>
{
    private readonly FamilyHubApiFactory _factory;

    public LowStockNotificationTests(FamilyHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Low_stock_first_aid_item_sends_one_reminder_and_rearms_after_restocking()
    {
        var client = SignedInClient("low-stock-owner");
        await Onboard(client, "Alex");
        var sender = _factory.Services.GetRequiredService<FakePushNotificationSender>();
        sender.Clear();

        await client.PostAsJsonAsync("/api/push-subscriptions", new
        {
            endpoint = "https://push.example.test/subscriptions/alex-low-stock-phone",
            p256dh = "browser-public-key",
            auth = "browser-auth-secret",
            timeZoneId = "Asia/Jakarta",
        });
        var created = await (await client.PostAsJsonAsync("/api/first-aid-items", new
        {
            name = "Adhesive bandages",
            quantity = 2,
            lowStockThreshold = 5,
            expiresOn = new DateOnly(2028, 6, 30),
            leadTimeDays = 30,
            type = "Bandage",
        })).Content.ReadFromJsonAsync<FirstAidItemResponse>();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<LowStockReminderDispatcher>();
        var now = DateTimeOffset.UtcNow;

        await dispatcher.DispatchDueAsync(now);
        await dispatcher.DispatchDueAsync(now);

        var sent = Assert.Single(sender.Notifications);
        Assert.Equal("Low stock", sent.Notification.Title);
        Assert.Contains("Adhesive bandages", sent.Notification.Body);

        // Restock above the threshold: the delivery record clears, so a fresh drop notifies again.
        await client.PutAsJsonAsync($"/api/first-aid-items/{created!.ItemId}", new
        {
            name = "Adhesive bandages",
            quantity = 20,
            lowStockThreshold = 5,
            expiresOn = new DateOnly(2028, 6, 30),
            leadTimeDays = 30,
            type = "Bandage",
        });
        await dispatcher.DispatchDueAsync(now);
        Assert.Single(sender.Notifications);

        await client.PutAsJsonAsync($"/api/first-aid-items/{created.ItemId}", new
        {
            name = "Adhesive bandages",
            quantity = 1,
            lowStockThreshold = 5,
            expiresOn = new DateOnly(2028, 6, 30),
            leadTimeDays = 30,
            type = "Bandage",
        });
        await dispatcher.DispatchDueAsync(now);

        Assert.Equal(2, sender.Notifications.Count);
    }

    private HttpClient SignedInClient(string googleSubjectId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, googleSubjectId);
        return client;
    }

    private static async Task Onboard(HttpClient client, string displayName)
    {
        var response = await client.PostAsJsonAsync("/api/household", new { displayName });
        response.EnsureSuccessStatusCode();
    }

    private sealed record FirstAidItemResponse(Guid ItemId);
}
