using UniversalPOS.Domain.Sales;

namespace UniversalPOS.Application.Sales;

public record PaymentAuthorizationRequest(long CompanyId, long BranchId, decimal Amount, string? InstrumentToken);
public record PaymentAuthorizationResult(bool Success, string? ProviderReference, string? FailureReason);

/// <summary>
/// One implementation per PaymentMethod. A non-cash payment is never marked Completed
/// without a provider response confirming it — see docs/architecture.md §12. Real
/// gateway integrations (PayHere, a card network) are added behind this interface
/// later without touching sale/checkout logic.
/// </summary>
public interface IPaymentProvider
{
    PaymentMethod SupportedMethod { get; }
    Task<PaymentAuthorizationResult> AuthorizeAsync(PaymentAuthorizationRequest request, CancellationToken cancellationToken = default);
}
