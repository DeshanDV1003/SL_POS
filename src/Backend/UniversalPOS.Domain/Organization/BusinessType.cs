namespace UniversalPOS.Domain.Organization;

/// <summary>
/// Bitmask flags on a Branch. Non-exclusive: a chain can run a Restaurant branch and a
/// Retail branch under one Company. Gates which modules (tables/KOT, barcode checkout,
/// wholesale pricing) are surfaced and enforced for that branch.
/// </summary>
[Flags]
public enum BusinessType
{
    None = 0,
    Restaurant = 1 << 0,
    Cafe = 1 << 1,
    Bakery = 1 << 2,
    FastFood = 1 << 3,
    Retail = 1 << 4,
    Grocery = 1 << 5,
    Supermarket = 1 << 6,
    Wholesale = 1 << 7,
    Fashion = 1 << 8,
    Electronics = 1 << 9,
}
