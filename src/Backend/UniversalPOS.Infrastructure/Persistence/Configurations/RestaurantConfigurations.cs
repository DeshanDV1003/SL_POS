using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalPOS.Domain.Restaurant;

namespace UniversalPOS.Infrastructure.Persistence.Configurations;

public class FloorConfiguration : IEntityTypeConfiguration<Floor>
{
    public void Configure(EntityTypeBuilder<Floor> builder)
    {
        builder.Property(f => f.Name).HasMaxLength(100).IsRequired();

        builder.HasMany(f => f.Tables)
            .WithOne(t => t.Floor)
            .HasForeignKey(t => t.FloorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DiningTableConfiguration : IEntityTypeConfiguration<DiningTable>
{
    public void Configure(EntityTypeBuilder<DiningTable> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(t => new { t.BranchId, t.Name }).IsUnique();
    }
}

public class TableSessionConfiguration : IEntityTypeConfiguration<TableSession>
{
    public void Configure(EntityTypeBuilder<TableSession> builder)
    {
        builder.HasIndex(s => new { s.TableId, s.Status });
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.Property(o => o.ContactPhone).HasMaxLength(30);
        builder.Property(o => o.DeliveryAddress).HasMaxLength(300);
        builder.Property(o => o.DeliveryFee).HasPrecision(18, 2);

        builder.HasMany(o => o.Lines)
            .WithOne(l => l.Order)
            .HasForeignKey(l => l.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => new { o.BranchId, o.Status });
    }
}

public class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.Property(l => l.Quantity).HasPrecision(18, 3);
        builder.Property(l => l.Notes).HasMaxLength(300);
    }
}


public class KitchenStationConfiguration : IEntityTypeConfiguration<KitchenStation>
{
    public void Configure(EntityTypeBuilder<KitchenStation> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(s => new { s.BranchId, s.Name }).IsUnique();
    }
}

public class PreparationTicketConfiguration : IEntityTypeConfiguration<PreparationTicket>
{
    public void Configure(EntityTypeBuilder<PreparationTicket> builder)
    {
        builder.Property(t => t.TicketNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(t => new { t.BranchId, t.TicketNumber }).IsUnique();
        builder.HasIndex(t => new { t.KitchenStationId, t.Status });

        builder.HasMany(t => t.Lines)
            .WithOne(l => l.PreparationTicket)
            .HasForeignKey(l => l.PreparationTicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PreparationTicketLineConfiguration : IEntityTypeConfiguration<PreparationTicketLine>
{
    public void Configure(EntityTypeBuilder<PreparationTicketLine> builder)
    {
        builder.Property(l => l.Quantity).HasPrecision(18, 3);
        builder.Property(l => l.Notes).HasMaxLength(300);
    }
}
