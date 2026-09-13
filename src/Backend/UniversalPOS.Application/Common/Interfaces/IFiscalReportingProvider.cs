namespace UniversalPOS.Application.Common.Interfaces;

public record FiscalTransmissionRequest(long CompanyId, long BranchId, long SaleHeaderId, string Payload);
public record FiscalTransmissionResult(bool Success, string? ProviderResponse);

/// <summary>
/// Abstraction over real-time invoice reporting to IRD (RAMIS Web API). Sri Lanka is
/// actively rolling this out for VAT-registered businesses (see
/// docs/research-sri-lanka-pos.md) — every finalized sale enqueues a transmission
/// regardless of whether a real provider is configured, so enabling real reporting
/// later is a provider swap, not a change to the sale path.
/// </summary>
public interface IFiscalReportingProvider
{
    Task<FiscalTransmissionResult> TransmitAsync(FiscalTransmissionRequest request, CancellationToken cancellationToken = default);
}
