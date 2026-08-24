namespace FamilyHub.Api.Chores;

public class Chore
{
    public Guid Id { get; init; }
    public required Guid HouseholdId { get; init; }
    public required string Title { get; init; }
    public required Guid AssignedFamilyMemberId { get; init; }
    public ChoreRecurrence Recurrence { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public enum ChoreRecurrence
{
    OneOff,
}