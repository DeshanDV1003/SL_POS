namespace UniversalPOS.Domain.Common;

public abstract class AuditableEntity
{
    public long Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public long? CreatedByUserId { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public long? ModifiedByUserId { get; set; }

    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
