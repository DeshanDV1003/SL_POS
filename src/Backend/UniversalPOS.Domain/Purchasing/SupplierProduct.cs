namespace UniversalPOS.Domain.Purchasing;

/// <summary>A Product's known cost/reference from a given Supplier. A Product can have several; one may be preferred.</summary>
public class SupplierProduct
{
    public long Id { get; set; }
    public long SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public long ProductId { get; set; }
    public string? SupplierSku { get; set; }
    public decimal CostPrice { get; set; }
    public bool IsPreferred { get; set; }
}
