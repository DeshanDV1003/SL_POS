using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalPOS.Domain.Purchasing;

namespace UniversalPOS.Infrastructure.Persistence.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(s => new { s.CompanyId, s.Name });
    }
}

public class SupplierProductConfiguration : IEntityTypeConfiguration<SupplierProduct>
{
    public void Configure(EntityTypeBuilder<SupplierProduct> builder)
    {
        builder.Property(sp => sp.CostPrice).HasPrecision(18, 2);
        builder.Property(sp => sp.SupplierSku).HasMaxLength(50);

        builder.HasOne(sp => sp.Supplier)
            .WithMany()
            .HasForeignKey(sp => sp.SupplierId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(sp => new { sp.SupplierId, sp.ProductId }).IsUnique();
    }
}

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.Property(po => po.OrderNumber).HasMaxLength(30).IsRequired();
        builder.Property(po => po.Notes).HasMaxLength(500);

        builder.HasIndex(po => new { po.BranchId, po.OrderNumber }).IsUnique();

        builder.HasOne(po => po.Supplier)
            .WithMany()
            .HasForeignKey(po => po.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(po => po.Lines)
            .WithOne(l => l.PurchaseOrder)
            .HasForeignKey(l => l.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.Property(l => l.QuantityOrdered).HasPrecision(18, 3);
        builder.Property(l => l.QuantityReceived).HasPrecision(18, 3);
        builder.Property(l => l.UnitCost).HasPrecision(18, 2);
    }
}

public class GoodsReceivedNoteConfiguration : IEntityTypeConfiguration<GoodsReceivedNote>
{
    public void Configure(EntityTypeBuilder<GoodsReceivedNote> builder)
    {
        builder.Property(g => g.GrnNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(g => new { g.BranchId, g.GrnNumber }).IsUnique();

        builder.HasMany(g => g.Lines)
            .WithOne(l => l.GoodsReceivedNote)
            .HasForeignKey(l => l.GoodsReceivedNoteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class GoodsReceivedNoteLineConfiguration : IEntityTypeConfiguration<GoodsReceivedNoteLine>
{
    public void Configure(EntityTypeBuilder<GoodsReceivedNoteLine> builder)
    {
        builder.Property(l => l.QuantityReceived).HasPrecision(18, 3);
        builder.Property(l => l.UnitCost).HasPrecision(18, 2);
        builder.Property(l => l.BatchNumber).HasMaxLength(50);
    }
}
