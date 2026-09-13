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
