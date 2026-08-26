namespace FamilyHub.Api.Warranties;

public class WarrantyFacet
{
    public required Guid ItemId { get; init; }
    public DateOnly? PurchasedOn { get; set; }
    public DateOnly? WarrantyExpiresOn { get; set; }
    public string? DocumentExternalId { get; set; }
}
