using FamilyHub.Api.Data;
using FamilyHub.Api.Medications;
using Microsoft.EntityFrameworkCore;

namespace FamilyHub.Api.Notifications;

public class MedicationReminderDispatcher(
    FamilyHubDbContext db,
    IPushNotificationSender sender)
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
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(candidate.Subscription.TimeZoneId);
            var localNow = TimeZoneInfo.ConvertTime(now, timeZone).DateTime;
            var scheduledOn = DateOnly.FromDateTime(localNow);
            var scheduledAt = scheduledOn.ToDateTime(candidate.Medication.ScheduledTime!.Value);
            var elapsed = localNow - scheduledAt;
            if (elapsed < TimeSpan.Zero || elapsed > DeliveryWindow)
            {
                continue;
            }

            var alreadySent = await db.MedicationReminderDeliveries.AnyAsync(delivery =>
                delivery.MedicationId == candidate.Medication.Id &&
                delivery.BrowserPushSubscriptionId == candidate.Subscription.Id &&
                delivery.ScheduledOn == scheduledOn,
                cancellationToken);
            if (alreadySent)
            {
                continue;
            }

            var notification = new PushNotification(
                "Medication reminder",
                $"{candidate.FamilyMemberName}: {candidate.Medication.Name} — {candidate.Medication.Dosage}",
                "/medications",
                $"medication-{candidate.Medication.Id}-{scheduledOn:yyyy-MM-dd}");
            await sender.SendAsync(candidate.Subscription, notification, cancellationToken);

            db.MedicationReminderDeliveries.Add(new MedicationReminderDelivery
            {
                Id = Guid.NewGuid(),
                MedicationId = candidate.Medication.Id,
                BrowserPushSubscriptionId = candidate.Subscription.Id,
                ScheduledOn = scheduledOn,
                SentAt = now,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}