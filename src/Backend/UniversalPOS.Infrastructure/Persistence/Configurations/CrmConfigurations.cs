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

        builder.HasIndex(c => new { c.CompanyId, c.Phone });
    }
}
