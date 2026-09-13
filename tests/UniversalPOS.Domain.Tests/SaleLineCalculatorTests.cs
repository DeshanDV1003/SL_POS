using FluentAssertions;
using UniversalPOS.Domain.Sales;
using Xunit;

namespace UniversalPOS.Domain.Tests;

public class SaleLineCalculatorTests
{
    [Fact]
    public void Calculate_NoTaxNoDiscount_ReturnsGrossAsTotal()
    {
        var result = SaleLineCalculator.Calculate(new SaleLineInput(2, 100m, 0, 0, TaxIsInclusive: false));

        result.GrossAmount.Should().Be(200m);
        result.DiscountAmount.Should().Be(0m);
        result.TaxAmount.Should().Be(0m);
        result.LineTotal.Should().Be(200m);
    }

    [Fact]
    public void Calculate_ExclusiveTax_AddsTaxOnTopOfNet()
    {
        // 1 x 100, 18% exclusive VAT -> 100 + 18 = 118
        var result = SaleLineCalculator.Calculate(new SaleLineInput(1, 100m, 0, 18m, TaxIsInclusive: false));

        result.NetAmount.Should().Be(100m);
        result.TaxAmount.Should().Be(18m);
        result.LineTotal.Should().Be(118m);
    }

    [Fact]
    public void Calculate_InclusiveTax_BacksTaxOutOfNet_TotalUnchanged()
    {
        // Price already includes 18% VAT: net=118 -> tax = 118 - 118/1.18 = 18, total stays 118.
        var result = SaleLineCalculator.Calculate(new SaleLineInput(1, 118m, 0, 18m, TaxIsInclusive: true));

        result.LineTotal.Should().Be(118m);
        result.TaxAmount.Should().Be(18m);
    }

    [Fact]
    public void Calculate_WithLineDiscount_ReducesNetBeforeTax()
    {
        // 1 x 1000, 10% discount -> net 900, no tax.
        var result = SaleLineCalculator.Calculate(new SaleLineInput(1, 1000m, 10m, 0, TaxIsInclusive: false));

        result.DiscountAmount.Should().Be(100m);
        result.NetAmount.Should().Be(900m);
        result.LineTotal.Should().Be(900m);
    }

    [Fact]
    public void Calculate_ZeroQuantity_ReturnsAllZeros()
    {
        var result = SaleLineCalculator.Calculate(new SaleLineInput(0, 500m, 0, 18m, TaxIsInclusive: false));

        result.LineTotal.Should().Be(0m);
    }

    [Fact]
    public void Calculate_NegativeQuantity_Throws()
    {
        var act = () => SaleLineCalculator.Calculate(new SaleLineInput(-1, 100m, 0, 0, false));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Calculate_DiscountOver100Percent_Throws()
    {
        var act = () => SaleLineCalculator.Calculate(new SaleLineInput(1, 100m, 150m, 0, false));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Calculate_FractionalCentRounding_RoundsHalfAwayFromZero()
    {
        // 3 x 33.335 = 100.005 -> rounds to 100.01, not 100.00 (banker's rounding would give 100.00).
        var result = SaleLineCalculator.Calculate(new SaleLineInput(3, 33.335m, 0, 0, false));

        result.GrossAmount.Should().Be(100.01m);
    }
}
