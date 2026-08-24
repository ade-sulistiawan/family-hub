namespace FamilyHub.Api.Warranties;

public class WarrantyFacet
{
    public required Guid ItemId { get; init; }
    public DateOnly? PurchasedOn { get; init; }
    public DateOnly? WarrantyExpiresOn { get; init; }
    public string? DocumentExternalId { get; init; }
}
