namespace UniversalPOS.Domain.Fiscal;

public enum FiscalTransmissionStatus
{
    Pending = 0,
    Sent = 1,
    Acknowledged = 2,
    Failed = 3,
}

/// <summary>
/// One row per finalized sale, enqueued regardless of whether a real IFiscalReportingProvider
/// is wired up. Scaffolded now because Sri Lanka's IRD is actively rolling out real-time
/// e-invoicing (RAMIS Web API) — see docs/research-sri-lanka-pos.md. The default
/// NullFiscalReportingProvider marks rows Acknowledged immediately; turning on real
/// transmission later is a provider swap, not a schema change.
/// </summary>
public class FiscalTransmission
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long BranchId { get; set; }

    /// <summary>FK to Sales.SaleHeader, added when the Sales module (Phase 6) introduces that table.</summary>
    public long SaleHeaderId { get; set; }

    public FiscalTransmissionStatus Status { get; set; } = FiscalTransmissionStatus.Pending;
    public string Payload { get; set; } = string.Empty;
    public string? ProviderResponse { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
