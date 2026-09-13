using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalPOS.Domain.Cash;
using UniversalPOS.Domain.Sales;

namespace UniversalPOS.Infrastructure.Persistence.Configurations;

public class SaleHeaderConfiguration : IEntityTypeConfiguration<SaleHeader>
{
    public void Configure(EntityTypeBuilder<SaleHeader> builder)
    {
        builder.Property(s => s.InvoiceNumber).HasMaxLength(30);
        builder.Property(s => s.PurchaserTin).HasMaxLength(20);
        builder.Property(s => s.PurchaserName).HasMaxLength(200);
        builder.Property(s => s.PurchaserAddress).HasMaxLength(300);
        builder.Property(s => s.VoidReason).HasMaxLength(300);
        builder.Property(s => s.ClientIdempotencyKey).HasMaxLength(100);

        builder.Property(s => s.SubTotal).HasPrecision(18, 2);
        builder.Property(s => s.DiscountTotal).HasPrecision(18, 2);
        builder.Property(s => s.TaxTotal).HasPrecision(18, 2);
        builder.Property(s => s.ServiceChargeTotal).HasPrecision(18, 2);
        builder.Property(s => s.GrandTotal).HasPrecision(18, 2);

        builder.HasIndex(s => new { s.BranchId, s.InvoiceNumber }).IsUnique().HasFilter("[InvoiceNumber] IS NOT NULL");
        builder.HasIndex(s => s.ClientIdempotencyKey).IsUnique().HasFilter("[ClientIdempotencyKey] IS NOT NULL");

        builder.HasOne<CashierShift>()
            .WithMany()
            .HasForeignKey(s => s.CashierShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Lines)
            .WithOne(l => l.SaleHeader)
            .HasForeignKey(l => l.SaleHeaderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Payments)
            .WithOne(p => p.SaleHeader)
            .HasForeignKey(p => p.SaleHeaderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SaleLineConfiguration : IEntityTypeConfiguration<SaleLine>
{
    public void Configure(EntityTypeBuilder<SaleLine> builder)
    {
        builder.Property(l => l.Quantity).HasPrecision(18, 3);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);
        builder.Property(l => l.DiscountPercentage).HasPrecision(5, 2);
        builder.Property(l => l.TaxRatePercentage).HasPrecision(5, 2);
        builder.Property(l => l.LineDiscountAmount).HasPrecision(18, 2);
        builder.Property(l => l.LineTaxAmount).HasPrecision(18, 2);
        builder.Property(l => l.LineTotal).HasPrecision(18, 2);
    }
}

public class SalePaymentConfiguration : IEntityTypeConfiguration<SalePayment>
{
    public void Configure(EntityTypeBuilder<SalePayment> builder)
    {
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.ProviderReference).HasMaxLength(100);
        builder.Property(p => p.ProviderStatus).HasMaxLength(50);
    }
}

public class HeldBillConfiguration : IEntityTypeConfiguration<HeldBill>
{
    public void Configure(EntityTypeBuilder<HeldBill> builder)
    {
        builder.Property(h => h.Notes).HasMaxLength(300);

        builder.HasMany(h => h.Lines)
            .WithOne(l => l.HeldBill)
            .HasForeignKey(l => l.HeldBillId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class HeldBillLineConfiguration : IEntityTypeConfiguration<HeldBillLine>
{
    public void Configure(EntityTypeBuilder<HeldBillLine> builder)
    {
        builder.Property(l => l.Quantity).HasPrecision(18, 3);
        builder.Property(l => l.DiscountPercentage).HasPrecision(5, 2);
    }
}

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
        builder.Property(p => p.DiscountValue).HasPrecision(18, 2);
        builder.Property(p => p.MinQuantity).HasPrecision(18, 3);

        builder.HasIndex(p => new { p.CompanyId, p.IsActive });
    }
}
