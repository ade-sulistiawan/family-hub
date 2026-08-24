namespace FamilyHub.Api.Notifications;

public class MedicationReminderDelivery
{
    public Guid Id { get; init; }
    public Guid MedicationId { get; init; }
    public Guid BrowserPushSubscriptionId { get; init; }
    public DateOnly ScheduledOn { get; init; }
    public DateTimeOffset SentAt { get; init; }
}