using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalPOS.Domain.Catalog;
using UniversalPOS.Domain.Restaurant;

namespace UniversalPOS.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();

        builder.HasOne(c => c.ParentCategory)
            .WithMany(c => c.SubCategories)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.CompanyId, c.Name });
    }
}

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.Property(b => b.Name).HasMaxLength(150).IsRequired();
        builder.HasIndex(b => new { b.CompanyId, b.Name }).IsUnique();
    }
}

public class UnitConfiguration : IEntityTypeConfiguration<Domain.Catalog.Unit>
{
    public void Configure(EntityTypeBuilder<Domain.Catalog.Unit> builder)
    {
        builder.Property(u => u.Name).HasMaxLength(50).IsRequired();
        builder.Property(u => u.Abbreviation).HasMaxLength(10).IsRequired();
        builder.Property(u => u.ConversionFactor).HasPrecision(18, 6);

        builder.HasOne<Domain.Catalog.Unit>()
            .WithMany()
            .HasForeignKey(u => u.BaseUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(u => new { u.CompanyId, u.Name }).IsUnique();
    }
}

public class TaxRateConfiguration : IEntityTypeConfiguration<TaxRate>
{
    public void Configure(EntityTypeBuilder<TaxRate> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Percentage).HasPrecision(5, 2);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.Property(p => p.Sku).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.CostPrice).HasPrecision(18, 2);
        builder.Property(p => p.SellingPrice).HasPrecision(18, 2);
        builder.Property(p => p.WholesalePrice).HasPrecision(18, 2);
        builder.Property(p => p.MinSellingPrice).HasPrecision(18, 2);
        builder.Property(p => p.ReorderLevel).HasPrecision(18, 3);
        builder.Property(p => p.MinStock).HasPrecision(18, 3);
        builder.Property(p => p.MaxStock).HasPrecision(18, 3);

        builder.HasIndex(p => new { p.CompanyId, p.Sku }).IsUnique();

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Brand>()
            .WithMany()
            .HasForeignKey(p => p.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Catalog.Unit>()
            .WithMany()
            .HasForeignKey(p => p.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<TaxRate>()
            .WithMany()
            .HasForeignKey(p => p.TaxRateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<KitchenStation>()
            .WithMany()
            .HasForeignKey(p => p.DefaultKitchenStationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Barcodes)
            .WithOne(b => b.Product)
            .HasForeignKey(b => b.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Variants)
            .WithOne(v => v.Product)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.ModifierGroups)
            .WithOne(g => g.Product)
            .HasForeignKey(g => g.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductBarcodeConfiguration : IEntityTypeConfiguration<ProductBarcode>
{
    public void Configure(EntityTypeBuilder<ProductBarcode> builder)
    {
        builder.Property(b => b.Barcode).HasMaxLength(64).IsRequired();
        builder.HasIndex(b => b.Barcode).IsUnique();
    }
}

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.Property(v => v.Name).HasMaxLength(100).IsRequired();
        builder.Property(v => v.Sku).HasMaxLength(50).IsRequired();
        builder.Property(v => v.PriceAdjustment).HasPrecision(18, 2);
        builder.HasIndex(v => v.Sku).IsUnique();
    }
}

public class ProductComponentConfiguration : IEntityTypeConfiguration<ProductComponent>
{
    public void Configure(EntityTypeBuilder<ProductComponent> builder)
    {
        builder.Property(c => c.Quantity).HasPrecision(18, 3);

        builder.HasOne(c => c.ParentProduct)
            .WithMany(p => p.Components)
            .HasForeignKey(c => c.ParentProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.ComponentProduct)
            .WithMany()
            .HasForeignKey(c => c.ComponentProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductModifierGroupConfiguration : IEntityTypeConfiguration<ProductModifierGroup>
{
    public void Configure(EntityTypeBuilder<ProductModifierGroup> builder)
    {
        builder.Property(g => g.Name).HasMaxLength(100).IsRequired();

        builder.HasMany(g => g.Modifiers)
            .WithOne(m => m.ModifierGroup)
            .HasForeignKey(m => m.ModifierGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductModifierConfiguration : IEntityTypeConfiguration<ProductModifier>
{
    public void Configure(EntityTypeBuilder<ProductModifier> builder)
    {
        builder.Property(m => m.Name).HasMaxLength(100).IsRequired();
        builder.Property(m => m.PriceAdjustment).HasPrecision(18, 2);
    }
}
