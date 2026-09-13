using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalPOS.Domain.Organization;

namespace UniversalPOS.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.LegalName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.DefaultCurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(c => c.TaxRegistrationNo).HasMaxLength(20);

        builder.HasMany(c => c.Branches)
            .WithOne(b => b.Company)
            .HasForeignKey(b => b.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.Property(b => b.Name).HasMaxLength(200).IsRequired();
        builder.Property(b => b.Code).HasMaxLength(20).IsRequired();
        builder.Property(b => b.ServiceChargeRate).HasPrecision(5, 4);

        builder.HasIndex(b => new { b.CompanyId, b.Code }).IsUnique();

        builder.HasMany(b => b.Terminals)
            .WithOne(t => t.Branch)
            .HasForeignKey(t => t.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TerminalConfiguration : IEntityTypeConfiguration<Terminal>
{
    public void Configure(EntityTypeBuilder<Terminal> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Code).HasMaxLength(20).IsRequired();

        builder.HasIndex(t => new { t.BranchId, t.Code }).IsUnique();
    }
}
