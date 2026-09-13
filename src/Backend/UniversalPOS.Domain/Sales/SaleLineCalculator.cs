namespace UniversalPOS.Domain.Sales;

public record SaleLineInput(
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercentage,
    decimal TaxRatePercentage,
    bool TaxIsInclusive);

public record SaleLineResult(
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal NetAmount,
    decimal TaxAmount,
    decimal LineTotal);

/// <summary>
/// The single place line-level sale math happens. Never duplicate this inline in a
/// controller or service — see docs/architecture.md §8.
/// </summary>
public static class SaleLineCalculator
{
    public static SaleLineResult Calculate(SaleLineInput input)
    {
        if (input.Quantity < 0) throw new ArgumentOutOfRangeException(nameof(input.Quantity), "Quantity cannot be negative.");
        if (input.UnitPrice < 0) throw new ArgumentOutOfRangeException(nameof(input.UnitPrice), "UnitPrice cannot be negative.");
        if (input.DiscountPercentage is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(input.DiscountPercentage), "DiscountPercentage must be between 0 and 100.");

        var gross = Money.Round(input.Quantity * input.UnitPrice);
        var discount = Money.Round(gross * input.DiscountPercentage / 100m);
        var net = gross - discount;

        decimal tax;
        decimal lineTotal;
        var rate = input.TaxRatePercentage / 100m;

        if (input.TaxIsInclusive)
        {
            // net already contains tax: back it out rather than adding on top of it.
            tax = Money.Round(net - net / (1 + rate));
            lineTotal = net;
        }
        else
        {
            tax = Money.Round(net * rate);
            lineTotal = net + tax;
        }

        return new SaleLineResult(gross, discount, net, tax, lineTotal);
    }
}
