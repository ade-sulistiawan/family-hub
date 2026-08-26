namespace FamilyHub.Api.Chores;

public class ChoreOccurrence
{
    public Guid Id { get; init; }
    public required Guid ChoreId { get; init; }
    public DateOnly ScheduledDate { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}