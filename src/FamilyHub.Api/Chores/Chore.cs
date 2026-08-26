namespace FamilyHub.Api.Chores;

public class Chore
{
    public Guid Id { get; init; }
    public required Guid HouseholdId { get; init; }
    public required string Title { get; set; }
    public required Guid AssignedFamilyMemberId { get; set; }
    public ChoreRecurrence Recurrence { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public enum ChoreRecurrence
{
    OneOff,
}