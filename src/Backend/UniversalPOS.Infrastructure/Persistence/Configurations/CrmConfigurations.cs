using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalPOS.Domain.Crm;

namespace UniversalPOS.Infrastructure.Persistence.Configurations;

public class CustomerGroupConfiguration : IEntityTypeConfiguration<CustomerGroup>
{
    public void Configure(EntityTypeBuilder<CustomerGroup> builder)
    {
        builder.Property(g => g.Name).HasMaxLength(100).IsRequired();
        builder.Property(g => g.DefaultDiscountPercentage).HasPrecision(5, 2);
        builder.HasIndex(g => new { g.CompanyId, g.Name }).IsUnique();
    }
}

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.CreditLimit).HasPrecision(18, 2);
        builder.Property(c => c.OutstandingBalance).HasPrecision(18, 2);

        builder.HasOne<CustomerGroup>()
            .WithMany()
            .HasForeignKey(c => c.CustomerGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<MembershipTier>()
            .WithMany()
            .HasForeignKey(c => c.MembershipTierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.CompanyId, c.Phone });
    }
}

public class MembershipTierConfiguration : IEntityTypeConfiguration<MembershipTier>
{
    public void Configure(EntityTypeBuilder<MembershipTier> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.Property(t => t.PointsMultiplier).HasPrecision(5, 2);
        builder.HasIndex(t => new { t.CompanyId, t.Name }).IsUnique();
    }
}

public class LoyaltyTransactionConfiguration : IEntityTypeConfiguration<LoyaltyTransaction>
{
    public void Configure(EntityTypeBuilder<LoyaltyTransaction> builder)
    {
        builder.Property(t => t.ReferenceType).HasMaxLength(50);
        builder.Property(t => t.Notes).HasMaxLength(300);
        builder.HasIndex(t => t.CustomerId);
    }
}
