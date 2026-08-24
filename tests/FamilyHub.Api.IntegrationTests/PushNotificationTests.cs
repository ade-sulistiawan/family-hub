using System.Net;
using System.Net.Http.Json;
using FamilyHub.Api.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyHub.Api.IntegrationTests;

public class PushNotificationTests : IClassFixture<FamilyHubApiFactory>
{
    private readonly FamilyHubApiFactory _factory;

    public PushNotificationTests(FamilyHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Family_member_can_register_a_push_subscription_that_other_households_cannot_remove()
    {
        var owner = SignedInClient("push-owner");
        await Onboard(owner, "Alex");
        var otherHousehold = SignedInClient("push-outsider");
        await Onboard(otherHousehold, "Morgan");
        var sender = _factory.Services.GetRequiredService<FakePushNotificationSender>();
        sender.Clear();

        var createResponse = await owner.PostAsJsonAsync("/api/push-subscriptions", new
        {
            endpoint = "https://push.example.test/subscriptions/alex-phone",
            p256dh = "browser-public-key",
            auth = "browser-auth-secret",
            timeZoneId = "Asia/Jakarta",
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var subscription = await createResponse.Content.ReadFromJsonAsync<PushSubscriptionResponse>();
        var outsiderTest = await otherHousehold.PostAsync(
            $"/api/push-subscriptions/{subscription!.PushSubscriptionId}/test",
            null);
        Assert.Equal(HttpStatusCode.NotFound, outsiderTest.StatusCode);

        var ownerTest = await owner.PostAsync(
            $"/api/push-subscriptions/{subscription.PushSubscriptionId}/test",
            null);
        Assert.Equal(HttpStatusCode.NoContent, ownerTest.StatusCode);
        var sent = Assert.Single(sender.Notifications);
        Assert.Equal("Family Hub notifications are ready", sent.Notification.Title);

        var outsiderDelete = await otherHousehold.DeleteAsync($"/api/push-subscriptions/{subscription!.PushSubscriptionId}");
        Assert.Equal(HttpStatusCode.NotFound, outsiderDelete.StatusCode);

        var ownerDelete = await owner.DeleteAsync($"/api/push-subscriptions/{subscription.PushSubscriptionId}");
        Assert.Equal(HttpStatusCode.NoContent, ownerDelete.StatusCode);
    }

    [Fact]
    public async Task Due_scheduled_medication_sends_one_push_notification_in_the_subscribers_time_zone()
    {
        var client = SignedInClient("scheduled-push-owner");
        var familyMemberId = await Onboard(client, "Alex");
        var sender = _factory.Services.GetRequiredService<FakePushNotificationSender>();
        sender.Clear();

        await client.PostAsJsonAsync("/api/push-subscriptions", new
        {
            endpoint = "https://push.example.test/subscriptions/alex-scheduled-phone",
            p256dh = "browser-public-key",
            auth = "browser-auth-secret",
            timeZoneId = "Asia/Jakarta",
        });
        await client.PostAsJsonAsync("/api/medications", new
        {
            name = "Vitamin D",
            dosage = "1 tablet",
            assignedFamilyMemberId = familyMemberId,
            kind = "Scheduled",
            scheduledTime = new TimeOnly(8, 0),
            minimumHoursBetweenDoses = (int?)null,
        });

        await using var scope = _factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<MedicationReminderDispatcher>();
        var now = new DateTimeOffset(2026, 8, 24, 1, 0, 30, TimeSpan.Zero);
        await dispatcher.DispatchDueAsync(now);
        await dispatcher.DispatchDueAsync(now);

        var sent = Assert.Single(sender.Notifications);
        Assert.Equal("Medication reminder", sent.Notification.Title);
        Assert.Equal("Alex: Vitamin D — 1 tablet", sent.Notification.Body);
        Assert.Equal("/medications", sent.Notification.Url);
    }

    private HttpClient SignedInClient(string googleSubjectId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, googleSubjectId);
        return client;
    }

    private static async Task<Guid> Onboard(HttpClient client, string displayName)
    {
        var response = await client.PostAsJsonAsync("/api/household", new { displayName });
        response.EnsureSuccessStatusCode();
        var household = await response.Content.ReadFromJsonAsync<HouseholdSetupResponse>();
        return household!.FamilyMemberId;
    }

    private sealed record HouseholdSetupResponse(Guid FamilyMemberId);
    private sealed record PushSubscriptionResponse(Guid PushSubscriptionId);
}