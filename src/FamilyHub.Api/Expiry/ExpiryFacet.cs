namespace FamilyHub.Api.Expiry;

public class ExpiryFacet
{
    public required Guid ItemId { get; init; }
    public DateOnly ExpiresOn { get; init; }
    public int LeadTimeDays { get; init; }
}
