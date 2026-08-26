namespace FamilyHub.Api.FirstAid;

public class StockFacet
{
    public required Guid ItemId { get; init; }
    public int Quantity { get; set; }
    public int LowStockThreshold { get; set; }
    public FirstAidItemType Type { get; set; }
}

public enum FirstAidItemType
{
    Other,
    Tablet,
    Syrup,
    Ointment,
    Spray,
    Bandage,
    Equipment,
}
