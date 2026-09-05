namespace ExpenseManagement.Domain.Common;

/// <summary>
/// Rounding and percentage helpers for currency. Centralised so that every
/// total in the system rounds the same way — banker's rounding is deliberately
/// avoided because users expect 0.005 to round up, not to even.
/// </summary>
public static class Money
{
    /// <summary>Scale used by the database column <c>decimal(18,2)</c>.</summary>
    public const int Scale = 2;

    public static decimal Round(decimal value) =>
        Math.Round(value, Scale, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Percentage of <paramref name="total"/> that <paramref name="part"/> represents,
    /// rounded to two places. A zero or negative total yields 0 rather than
    /// dividing by zero — a budget of 0 is "nothing to use up", not an error.
    /// </summary>
    public static decimal Percentage(decimal part, decimal total)
    {
        if (total <= 0m) return 0m;
        return Math.Round(part / total * 100m, Scale, MidpointRounding.AwayFromZero);
    }

    /// <summary>Remaining headroom, floored at nothing-below-zero being meaningful to the caller.</summary>
    public static decimal Remaining(decimal budget, decimal spent) => Round(budget - spent);
}
