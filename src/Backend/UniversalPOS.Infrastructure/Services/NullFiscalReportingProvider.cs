using Microsoft.Extensions.Logging;
using UniversalPOS.Application.Common.Interfaces;

namespace UniversalPOS.Infrastructure.Services;

/// <summary>
/// Default IFiscalReportingProvider until a real IRD RAMIS Web API integration is
/// contracted. Logs intent and reports success so the FiscalTransmission queue stays
/// exercised end-to-end; never claims a real transmission occurred.
/// </summary>
public class NullFiscalReportingProvider : IFiscalReportingProvider
{
    private readonly ILogger<NullFiscalReportingProvider> _logger;

    public NullFiscalReportingProvider(ILogger<NullFiscalReportingProvider> logger)
    {
        _logger = logger;
    }

    public Task<FiscalTransmissionResult> TransmitAsync(FiscalTransmissionRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Fiscal reporting is not yet configured (no real IRD provider wired up). Sale {SaleHeaderId} for branch {BranchId} was NOT transmitted to any tax authority.",
            request.SaleHeaderId, request.BranchId);

        return Task.FromResult(new FiscalTransmissionResult(Success: true, ProviderResponse: "no-op: no fiscal provider configured"));
    }
}
