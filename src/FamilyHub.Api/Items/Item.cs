namespace FamilyHub.Api.Items;

public class Item
{
    public Guid Id { get; init; }
    public required Guid HouseholdId { get; init; }
    public required string Name { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
