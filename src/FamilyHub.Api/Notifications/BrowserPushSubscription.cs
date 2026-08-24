namespace FamilyHub.Api.Notifications;

public class BrowserPushSubscription
{
    public Guid Id { get; init; }
    public Guid FamilyMemberId { get; init; }
    public required string Endpoint { get; init; }
    public required string P256dh { get; set; }
    public required string Auth { get; set; }
    public required string TimeZoneId { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}