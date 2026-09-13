namespace UniversalPOS.Domain.Sales;

public record PaymentLineInput(PaymentMethod Method, decimal Amount);

public record PaymentAllocationResult(decimal TotalTendered, decimal ChangeDue, bool IsFullyPaid);

public class PaymentValidationException : Exception
{
    public PaymentValidationException(string message) : base(message) { }
}

/// <summary>
/// Validates and reconciles a sale's tendered payments against its grand total.
/// Overpayment (producing change) is only meaningful for cash — a non-cash instrument
/// cannot hand back physical change, so overpaying with one is rejected rather than
/// silently generating a "change" figure nobody can actually give the customer.
/// </summary>
public static class PaymentAllocator
{
    public static PaymentAllocationResult Allocate(decimal grandTotal, IReadOnlyList<PaymentLineInput> payments)
    {
        if (payments.Count == 0)
        {
            throw new PaymentValidationException("At least one payment line is required.");
        }

        if (payments.Any(p => p.Amount <= 0))
        {
            throw new PaymentValidationException("Payment amounts must be positive.");
        }

        var totalTendered = Money.Round(payments.Sum(p => p.Amount));
        var nonCashTendered = Money.Round(payments.Where(p => p.Method != PaymentMethod.Cash).Sum(p => p.Amount));

        if (nonCashTendered > grandTotal)
        {
            throw new PaymentValidationException("Non-cash payment methods cannot exceed the sale total (no change can be issued for card/digital/bank transfer).");
        }

        if (totalTendered < grandTotal)
        {
            return new PaymentAllocationResult(totalTendered, 0m, IsFullyPaid: false);
        }

        var changeDue = Money.Round(totalTendered - grandTotal);
        return new PaymentAllocationResult(totalTendered, changeDue, IsFullyPaid: true);
    }
}
