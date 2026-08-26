namespace FamilyHub.Api.Expiry;

public class ExpiryFacet
{
    public required Guid ItemId { get; init; }
    public DateOnly ExpiresOn { get; set; }
    public int LeadTimeDays { get; set; }
}
