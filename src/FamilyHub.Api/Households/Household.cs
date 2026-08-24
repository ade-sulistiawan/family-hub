namespace FamilyHub.Api.Households;

/// <summary>The family unit that owns all data in one Family Hub deployment.</summary>
public class Household
{
    public Guid Id { get; init; }
    public required string JoinCode { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
