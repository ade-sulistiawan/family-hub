namespace FamilyHub.Api.FirstAid;

public class StockFacet
{
    public required Guid ItemId { get; init; }
    public int Quantity { get; set; }
    public int LowStockThreshold { get; init; }
}
