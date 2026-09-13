using UniversalPOS.Application.Sales;
using UniversalPOS.Domain.Sales;

namespace UniversalPOS.Infrastructure.Payments;

/// <summary>Cash needs no external authorization — it's real money already in the drawer.</summary>
public class CashPaymentProvider : IPaymentProvider
{
    public PaymentMethod SupportedMethod => PaymentMethod.Cash;

    public Task<PaymentAuthorizationResult> AuthorizeAsync(PaymentAuthorizationRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new PaymentAuthorizationResult(Success: true, ProviderReference: "CASH", FailureReason: null));
}
