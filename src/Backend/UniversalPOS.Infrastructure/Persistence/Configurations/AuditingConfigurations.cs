using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalPOS.Domain.Auditing;
using UniversalPOS.Domain.Fiscal;

namespace UniversalPOS.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(a => a.ActionCode).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(50).IsRequired();

        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => a.CreatedAtUtc);
    }
}

public class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        builder.Property(a => a.ActionCode).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(50).IsRequired();
    }
}

public class FiscalTransmissionConfiguration : IEntityTypeConfiguration<FiscalTransmission>
{
    public void Configure(EntityTypeBuilder<FiscalTransmission> builder)
    {
        builder.Property(f => f.Payload).IsRequired();
        builder.HasIndex(f => f.SaleHeaderId);
        builder.HasIndex(f => f.Status);
    }
}
