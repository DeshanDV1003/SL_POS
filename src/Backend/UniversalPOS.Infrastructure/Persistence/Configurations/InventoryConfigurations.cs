using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalPOS.Domain.Inventory;

namespace UniversalPOS.Infrastructure.Persistence.Configurations;

public class StockLedgerConfiguration : IEntityTypeConfiguration<StockLedger>
{
    public void Configure(EntityTypeBuilder<StockLedger> builder)
    {
        builder.Property(s => s.QuantityChange).HasPrecision(18, 3);
        builder.Property(s => s.ReferenceType).HasMaxLength(50).IsRequired();

        builder.HasIndex(s => new { s.BranchId, s.ProductId });
        builder.HasIndex(s => new { s.ReferenceType, s.ReferenceId });
        builder.HasIndex(s => s.CreatedAtUtc);
    }
}

public class StockOnHandConfiguration : IEntityTypeConfiguration<StockOnHand>
{
    public void Configure(EntityTypeBuilder<StockOnHand> builder)
    {
        builder.Property(s => s.QuantityOnHand).HasPrecision(18, 3);
        builder.Property(s => s.RowVersion).IsRowVersion();

        builder.HasIndex(s => new { s.BranchId, s.ProductId }).IsUnique();
    }
}

public class ProductBatchConfiguration : IEntityTypeConfiguration<ProductBatch>
{
    public void Configure(EntityTypeBuilder<ProductBatch> builder)
    {
        builder.Property(b => b.BatchNumber).HasMaxLength(50).IsRequired();
        builder.Property(b => b.QuantityReceived).HasPrecision(18, 3);
        builder.Property(b => b.QuantityRemaining).HasPrecision(18, 3);

        builder.HasIndex(b => new { b.BranchId, b.ProductId, b.BatchNumber }).IsUnique();
    }
}

public class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> builder)
    {
        builder.Property(a => a.Notes).HasMaxLength(500);

        builder.HasMany(a => a.Lines)
            .WithOne(l => l.StockAdjustment)
            .HasForeignKey(l => l.StockAdjustmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class StockAdjustmentLineConfiguration : IEntityTypeConfiguration<StockAdjustmentLine>
{
    public void Configure(EntityTypeBuilder<StockAdjustmentLine> builder)
    {
        builder.Property(l => l.QuantityChange).HasPrecision(18, 3);
    }
}

public class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> builder)
    {
        builder.HasIndex(t => new { t.FromBranchId, t.Status });
        builder.HasIndex(t => new { t.ToBranchId, t.Status });

        builder.HasMany(t => t.Lines)
            .WithOne(l => l.StockTransfer)
            .HasForeignKey(l => l.StockTransferId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class StockTransferLineConfiguration : IEntityTypeConfiguration<StockTransferLine>
{
    public void Configure(EntityTypeBuilder<StockTransferLine> builder)
    {
        builder.Property(l => l.Quantity).HasPrecision(18, 3);
    }
}

public class StockCountConfiguration : IEntityTypeConfiguration<StockCount>
{
    public void Configure(EntityTypeBuilder<StockCount> builder)
    {
        builder.HasIndex(c => new { c.BranchId, c.Status });

        builder.HasMany(c => c.Lines)
            .WithOne(l => l.StockCount)
            .HasForeignKey(l => l.StockCountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class StockCountLineConfiguration : IEntityTypeConfiguration<StockCountLine>
{
    public void Configure(EntityTypeBuilder<StockCountLine> builder)
    {
        builder.Property(l => l.SystemQuantity).HasPrecision(18, 3);
        builder.Property(l => l.CountedQuantity).HasPrecision(18, 3);
    }
}
