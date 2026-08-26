using FamilyHub.Api.Data;
using FamilyHub.Api.FirstAid;
using Microsoft.EntityFrameworkCore;

namespace FamilyHub.Api.Notifications;

public class LowStockReminderDispatcher(
    FamilyHubDbContext db,
    IPushNotificationSender sender)
{
    public async Task DispatchDueAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var stockItems = await (
            from item in db.Items.AsNoTracking()
            join stock in db.StockFacets.AsNoTracking() on item.Id equals stock.ItemId
            select new { Item = item, Stock = stock })
            .ToListAsync(cancellationToken);

        var subscriptionsByHousehold = (await (
            from familyMember in db.FamilyMembers.AsNoTracking()
            join subscription in db.BrowserPushSubscriptions.AsNoTracking()
                on familyMember.Id equals subscription.FamilyMemberId
            select new { familyMember.HouseholdId, Subscription = subscription })
            .ToListAsync(cancellationToken))
            .ToLookup(row => row.HouseholdId, row => row.Subscription);

        var existingDeliveries = (await db.LowStockNotificationDeliveries
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .ToLookup(delivery => delivery.ItemId, delivery => delivery.BrowserPushSubscriptionId);

        foreach (var stockItem in stockItems)
        {
            if (stockItem.Stock.Quantity > stockItem.Stock.LowStockThreshold)
            {
                // Restocked: clear any deliveries so a future low-stock episode notifies again.
                if (existingDeliveries[stockItem.Item.Id].Any())
                {
                    var staleDeliveries = db.LowStockNotificationDeliveries
                        .Where(delivery => delivery.ItemId == stockItem.Item.Id);
                    db.LowStockNotificationDeliveries.RemoveRange(staleDeliveries);
                    await db.SaveChangesAsync(cancellationToken);
                }
                continue;
            }

            foreach (var subscription in subscriptionsByHousehold[stockItem.Item.HouseholdId])
            {
                if (existingDeliveries[stockItem.Item.Id].Contains(subscription.Id))
                {
                    continue;
                }

                var notification = new PushNotification(
                    "Low stock",
                    $"{stockItem.Item.Name}: {stockItem.Stock.Quantity} left (min {stockItem.Stock.LowStockThreshold})",
                    "/first-aid",
                    $"low-stock-{stockItem.Item.Id}");
                await sender.SendAsync(subscription, notification, cancellationToken);

                db.LowStockNotificationDeliveries.Add(new LowStockNotificationDelivery
                {
                    Id = Guid.NewGuid(),
                    ItemId = stockItem.Item.Id,
                    BrowserPushSubscriptionId = subscription.Id,
                    SentAt = now,
                });
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
