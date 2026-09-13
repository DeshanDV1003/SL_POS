namespace UniversalPOS.Domain.Restaurant;

public enum StationCategory
{
    Kitchen = 0,
    Bar = 1,
    Dessert = 2,
    Other = 3,
}

/// <summary>e.g. "Main Kitchen", "Bar". Products route to a station via Product.DefaultKitchenStationId.</summary>
public class KitchenStation
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public StationCategory Category { get; set; } = StationCategory.Kitchen;
    public bool IsActive { get; set; } = true;
}
