namespace UniversalPOS.Domain.Sales;

/// <summary>
/// Centralizes rounding so it happens in exactly one place. Standard "round half away
/// from zero" to 2 decimal places, matching customer-facing cash expectations — never
/// banker's rounding, which would silently under/over-charge relative to what a
/// printed receipt shows. See docs/architecture.md §8.
/// </summary>
public static class Money
{
    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
