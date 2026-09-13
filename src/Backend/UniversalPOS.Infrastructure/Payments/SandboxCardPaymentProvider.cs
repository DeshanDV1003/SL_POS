using UniversalPOS.Application.Sales;
using UniversalPOS.Domain.Sales;

namespace UniversalPOS.Infrastructure.Payments;

/// <summary>
/// Deterministic test-mode provider used until a real gateway (e.g. PayHere — see
/// docs/research-sri-lanka-pos.md §2) is contracted with real merchant credentials.
/// Never claims a real card was actually charged: responses are clearly sandbox-only
/// and driven by a documented test-token convention, not randomness.
/// </summary>
public class SandboxCardPaymentProvider : IPaymentProvider
{
    public PaymentMethod SupportedMethod => PaymentMethod.Card;

    public Task<PaymentAuthorizationResult> AuthorizeAsync(PaymentAuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        // Test-token convention: a token starting with "4000" always declines (mirrors
        // the same convention real sandboxes like Stripe's use), anything else approves.
        if (request.InstrumentToken is not null && request.InstrumentToken.StartsWith("4000", StringComparison.Ordinal))
        {
            return Task.FromResult(new PaymentAuthorizationResult(Success: false, ProviderReference: null, FailureReason: "Card declined (sandbox test token)."));
        }

        var reference = $"SANDBOX-{Guid.NewGuid():N}"[..24];
        return Task.FromResult(new PaymentAuthorizationResult(Success: true, ProviderReference: reference, FailureReason: null));
    }
}
