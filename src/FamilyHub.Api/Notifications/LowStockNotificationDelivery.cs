namespace FamilyHub.Api.Notifications;

// One row per (Item, subscription) while stock stays low; deleted once the item is restocked
// above its threshold so a future low-stock episode can notify again.
public class LowStockNotificationDelivery
{
    public Guid Id { get; init; }
    public Guid ItemId { get; init; }
    public Guid BrowserPushSubscriptionId { get; init; }
    public DateTimeOffset SentAt { get; init; }
}
