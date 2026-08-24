namespace FamilyHub.Api.Households;

/// <summary>An individual person, identified by their Google account, belonging to exactly one Household.</summary>
public class FamilyMember
{
    public Guid Id { get; init; }
    public required Guid HouseholdId { get; init; }
    public required string GoogleSubjectId { get; init; }
    public required string DisplayName { get; init; }
    public required string Email { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
