namespace NickeltownFinance.Core.FinancialYears;

/// <summary>
/// Derives Australian-style financial year periods from transaction dates.
/// Financial years are never chosen by the user — only computed from dates.
/// </summary>
public static class FinancialYearPeriod
{
    public const int DefaultStartMonth = 7;
    public const int DefaultStartDay = 1;

    /// <summary>
    /// Returns the inclusive start and end dates and display name for the financial year
    /// containing <paramref name="date"/>.
    /// Example: 1 Jul 2026 and 30 Jun 2027 → "2026-2027".
    /// </summary>
    public static (DateTime OpeningDate, DateTime EndDate, string Name) ForDate(
        DateTime date,
        int startMonth = DefaultStartMonth,
        int startDay = DefaultStartDay)
    {
        startMonth = Math.Clamp(startMonth, 1, 12);
        startDay = Math.Clamp(startDay, 1, DateTime.DaysInMonth(2001, startMonth));

        var d = date.Date;
        var openingYear = d.Month > startMonth || (d.Month == startMonth && d.Day >= startDay)
            ? d.Year
            : d.Year - 1;

        var openingDate = SafeDate(openingYear, startMonth, startDay);
        var endDate = openingDate.AddYears(1).AddDays(-1);
        var name = FormatName(openingDate.Year, endDate.Year);
        return (openingDate, endDate, name);
    }

    public static string FormatName(int openingYear, int endYear) =>
        $"{openingYear}-{endYear}";

    public static string FormatName(DateTime openingDate) =>
        FormatName(openingDate.Year, openingDate.AddYears(1).AddDays(-1).Year);

    private static DateTime SafeDate(int year, int month, int day)
    {
        var maxDay = DateTime.DaysInMonth(year, month);
        return new DateTime(year, month, Math.Min(day, maxDay));
    }
}
