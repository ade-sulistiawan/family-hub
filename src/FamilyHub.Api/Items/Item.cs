namespace FamilyHub.Api.Items;

public class Item
{
    public Guid Id { get; init; }
    public required Guid HouseholdId { get; init; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}
