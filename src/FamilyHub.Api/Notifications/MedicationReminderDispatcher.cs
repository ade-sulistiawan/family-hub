using FamilyHub.Api.Data;
using FamilyHub.Api.Medications;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace FamilyHub.Api.Notifications;

public class MedicationReminderDispatcher(
    FamilyHubDbContext db,
    IPushNotificationSender sender,
    ILogger<MedicationReminderDispatcher> logger)
{
    private static readonly TimeSpan DeliveryWindow = TimeSpan.FromMinutes(2);

    public async Task DispatchDueAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var candidates = await (
            from medication in db.Medications.AsNoTracking()
            join familyMember in db.FamilyMembers.AsNoTracking()
                on medication.AssignedFamilyMemberId equals familyMember.Id
            join subscription in db.BrowserPushSubscriptions.AsNoTracking()
                on familyMember.Id equals subscription.FamilyMemberId
            where medication.Kind == MedicationKind.Scheduled && medication.ScheduledTime != null
            select new { Medication = medication, FamilyMemberName = familyMember.DisplayName, Subscription = subscription })
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            try
            {
                await DispatchCandidateAsync(candidate.Medication, candidate.FamilyMemberName, candidate.Subscription, now, cancellationToken);
            }
            catch (WebPushException exception)
            {
                // One bad/expired subscription must not stop reminders for every other medication in this pass.
                logger.LogWarning(
                    exception,
                    "Medication reminder push failed for subscription {SubscriptionId} ({StatusCode}).",
                    candidate.Subscription.Id,
                    exception.StatusCode);

                if (exception.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound)
                {
                    db.BrowserPushSubscriptions.Remove(candidate.Subscription);
                    await db.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Medication reminder dispatch failed for medication {MedicationId}.",
                    candidate.Medication.Id);
            }
        }
    }

    private async Task DispatchCandidateAsync(
        Medication medication,
        string familyMemberName,
        BrowserPushSubscription subscription,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(subscription.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(now, timeZone).DateTime;
        var scheduledOn = DateOnly.FromDateTime(localNow);
        var scheduledAt = scheduledOn.ToDateTime(medication.ScheduledTime!.Value);
        var elapsed = localNow - scheduledAt;
        if (elapsed < TimeSpan.Zero || elapsed > DeliveryWindow)
        {
            return;
        }

        var alreadySent = await db.MedicationReminderDeliveries.AnyAsync(delivery =>
            delivery.MedicationId == medication.Id &&
            delivery.BrowserPushSubscriptionId == subscription.Id &&
            delivery.ScheduledOn == scheduledOn,
            cancellationToken);
        if (alreadySent)
        {
            return;
        }

        var notification = new PushNotification(
            "Medication reminder",
            $"{familyMemberName}: {medication.Name} — {medication.Dosage}",
            "/medications",
            $"medication-{medication.Id}-{scheduledOn:yyyy-MM-dd}");
        await sender.SendAsync(subscription, notification, cancellationToken);

        db.MedicationReminderDeliveries.Add(new MedicationReminderDelivery
        {
            Id = Guid.NewGuid(),
            MedicationId = medication.Id,
            BrowserPushSubscriptionId = subscription.Id,
            ScheduledOn = scheduledOn,
            SentAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}