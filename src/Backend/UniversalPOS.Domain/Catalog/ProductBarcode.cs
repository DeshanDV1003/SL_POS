namespace UniversalPOS.Domain.Catalog;

/// <summary>A Product may have several printed barcodes (case pack, promotional resticker, etc).</summary>
public class ProductBarcode
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string Barcode { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}
