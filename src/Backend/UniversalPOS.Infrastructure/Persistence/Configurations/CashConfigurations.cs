using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalPOS.Domain.Cash;

namespace UniversalPOS.Infrastructure.Persistence.Configurations;

public class CashierShiftConfiguration : IEntityTypeConfiguration<CashierShift>
{
    public void Configure(EntityTypeBuilder<CashierShift> builder)
    {
        builder.Property(s => s.OpeningFloat).HasPrecision(18, 2);
        builder.Property(s => s.ClosingFloatCounted).HasPrecision(18, 2);
        builder.Property(s => s.ExpectedCash).HasPrecision(18, 2);
        builder.Property(s => s.VarianceAmount).HasPrecision(18, 2);

        builder.HasIndex(s => new { s.TerminalId, s.Status });
        builder.HasIndex(s => new { s.CashierUserId, s.Status });

        builder.HasMany(s => s.Movements)
            .WithOne(m => m.CashierShift)
            .HasForeignKey(m => m.CashierShiftId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> builder)
    {
        builder.Property(m => m.Amount).HasPrecision(18, 2);
        builder.Property(m => m.Reason).HasMaxLength(300);
    }
}

public class DayEndReportConfiguration : IEntityTypeConfiguration<DayEndReport>
{
    public void Configure(EntityTypeBuilder<DayEndReport> builder)
    {
        builder.Property(r => r.GrossSales).HasPrecision(18, 2);
        builder.Property(r => r.DiscountTotal).HasPrecision(18, 2);
        builder.Property(r => r.TaxTotal).HasPrecision(18, 2);
        builder.Property(r => r.ServiceChargeTotal).HasPrecision(18, 2);
        builder.Property(r => r.NetSales).HasPrecision(18, 2);
        builder.Property(r => r.RefundTotal).HasPrecision(18, 2);
        builder.Property(r => r.CashSalesTotal).HasPrecision(18, 2);
        builder.Property(r => r.CardSalesTotal).HasPrecision(18, 2);
        builder.Property(r => r.OtherPaymentTotal).HasPrecision(18, 2);

        builder.HasIndex(r => new { r.BranchId, r.BusinessDate }).IsUnique();
    }
}
