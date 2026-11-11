namespace NickeltownFinance.Core.FinancialYears;

/// <summary>
/// ANZ statement cycle used by Nickeltown: the 22nd of one month through the 21st of the next.
/// A report labelled for a calendar month covers the statement that <em>ends</em> on the 21st of that month.
/// Example: September 2026 → 22 August 2026 – 21 September 2026.
/// </summary>
public static class StatementPeriod
{
    public const int StartDay = 22;
    public const int EndDay = 21;

    /// <summary>
    /// Inclusive start and end dates for the statement named by <paramref name="year"/>/<paramref name="month"/>.
    /// </summary>
    public static (DateTime Start, DateTime End) ForMonth(int year, int month)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(month, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(month, 12);

        var end = new DateTime(year, month, EndDay);
        var previous = end.AddMonths(-1);
        var start = new DateTime(previous.Year, previous.Month, StartDay);
        return (start, end);
    }

    public static bool Matches(DateTime start, DateTime end, int year, int month)
    {
        var (expectedStart, expectedEnd) = ForMonth(year, month);
        return start.Date == expectedStart && end.Date == expectedEnd;
    }
}
