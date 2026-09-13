using FluentAssertions;
using UniversalPOS.Domain.Sales;
using Xunit;

namespace UniversalPOS.Domain.Tests;

public class PaymentAllocatorTests
{
    [Fact]
    public void Allocate_ExactCash_NoChangeDue()
    {
        var result = PaymentAllocator.Allocate(1000m, new[] { new PaymentLineInput(PaymentMethod.Cash, 1000m) });

        result.IsFullyPaid.Should().BeTrue();
        result.ChangeDue.Should().Be(0m);
    }

    [Fact]
    public void Allocate_CashOverpayment_ReturnsChange()
    {
        var result = PaymentAllocator.Allocate(950m, new[] { new PaymentLineInput(PaymentMethod.Cash, 1000m) });

        result.ChangeDue.Should().Be(50m);
    }

    [Fact]
    public void Allocate_CardOverpayment_Throws()
    {
        var act = () => PaymentAllocator.Allocate(500m, new[] { new PaymentLineInput(PaymentMethod.Card, 1000m) });

        act.Should().Throw<PaymentValidationException>();
    }

    [Fact]
    public void Allocate_SplitCashAndCard_ExactTotal_Succeeds()
    {
        var result = PaymentAllocator.Allocate(1000m, new[]
        {
            new PaymentLineInput(PaymentMethod.Card, 600m),
            new PaymentLineInput(PaymentMethod.Cash, 400m),
        });

        result.IsFullyPaid.Should().BeTrue();
        result.ChangeDue.Should().Be(0m);
    }

    [Fact]
    public void Allocate_Underpayment_IsNotFullyPaidAndNoChange()
    {
        var result = PaymentAllocator.Allocate(1000m, new[] { new PaymentLineInput(PaymentMethod.Cash, 400m) });

        result.IsFullyPaid.Should().BeFalse();
        result.ChangeDue.Should().Be(0m);
    }

    [Fact]
    public void Allocate_NoPayments_Throws()
    {
        var act = () => PaymentAllocator.Allocate(100m, Array.Empty<PaymentLineInput>());

        act.Should().Throw<PaymentValidationException>();
    }

    [Fact]
    public void Allocate_NegativeOrZeroPaymentAmount_Throws()
    {
        var act = () => PaymentAllocator.Allocate(100m, new[] { new PaymentLineInput(PaymentMethod.Cash, 0m) });

        act.Should().Throw<PaymentValidationException>();
    }
}
