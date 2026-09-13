namespace UniversalPOS.Domain.Catalog;

/// <summary>
/// A line in a composite/bundle Product's bill of materials. At sale time, a composite
/// product's stock deduction walks these rows against the component Products' own
/// StockLedger — the composite itself is never independently stocked.
/// </summary>
public class ProductComponent
{
    public long Id { get; set; }

    public long ParentProductId { get; set; }
    public Product ParentProduct { get; set; } = null!;

    public long ComponentProductId { get; set; }
    public Product ComponentProduct { get; set; } = null!;

    public decimal Quantity { get; set; }
}
