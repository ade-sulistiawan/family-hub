namespace FamilyHub.Api.Chores;

public class ChoreOccurrence
{
    public Guid Id { get; init; }
    public required Guid ChoreId { get; init; }
    public DateOnly ScheduledDate { get; init; }
    public DateTimeOffset? CompletedAt { get; set; }
}