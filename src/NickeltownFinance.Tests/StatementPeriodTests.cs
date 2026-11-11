using NickeltownFinance.Core.FinancialYears;
using NickeltownFinance.Core.Ledger;
using Xunit;

namespace NickeltownFinance.Tests;

public class StatementPeriodTests
{
    [Fact]
    public void ForMonth_UsesTwentySecondToTwentyFirst()
    {
        var (start, end) = StatementPeriod.ForMonth(2026, 9);

        Assert.Equal(new DateTime(2026, 8, 22), start);
        Assert.Equal(new DateTime(2026, 9, 21), end);
    }

    [Fact]
    public void ForMonth_JanuaryWrapsToPreviousYear()
    {
        var (start, end) = StatementPeriod.ForMonth(2027, 1);

        Assert.Equal(new DateTime(2026, 12, 22), start);
        Assert.Equal(new DateTime(2027, 1, 21), end);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void ForMonth_RejectsInvalidMonth(int month)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StatementPeriod.ForMonth(2026, month));
    }

    [Fact]
    public void Matches_DetectsStatementCycle()
    {
        Assert.True(StatementPeriod.Matches(new DateTime(2026, 8, 22), new DateTime(2026, 9, 21), 2026, 9));
        Assert.False(StatementPeriod.Matches(new DateTime(2026, 9, 1), new DateTime(2026, 9, 30), 2026, 9));
    }
}

public class BankBalanceTests
{
    [Fact]
    public void OpeningPlusPeriodNet_EqualsClosing_ForStatementCycle()
    {
        var trackingDate = new DateTime(2026, 7, 1);
        const decimal trackingBalance = 1000m;
        var (start, end) = StatementPeriod.ForMonth(2026, 9);

        (DateTime Date, decimal NetAmount)[] txns =
        [
            (new DateTime(2026, 8, 21), 50m),   // before period — in opening only
            (new DateTime(2026, 8, 22), 200m),  // first day of period
            (new DateTime(2026, 9, 10), -75m),
            (new DateTime(2026, 9, 21), 25m),   // last day of period
            (new DateTime(2026, 9, 22), 999m)   // after period — excluded
        ];

        var opening = BankBalance.AsOf(start.AddDays(-1), trackingDate, trackingBalance, txns);
        var closing = BankBalance.AsOf(end, trackingDate, trackingBalance, txns);
        var periodNet = txns.Where(t => t.Date >= start && t.Date <= end).Sum(t => t.NetAmount);

        Assert.Equal(1050m, opening);
        Assert.Equal(150m, periodNet);
        Assert.Equal(opening + periodNet, closing);
        Assert.Equal(1200m, closing);
    }

    [Fact]
    public void AsOf_TrackingDate_ReturnsTrackingBalance()
    {
        var tracking = new DateTime(2026, 7, 22);
        (DateTime Date, decimal NetAmount)[] txns =
        [
            (tracking, 40m),
            (tracking.AddDays(1), 10m)
        ];

        Assert.Equal(500m, BankBalance.AsOf(tracking, tracking, 500m, txns));
        Assert.Equal(510m, BankBalance.AsOf(tracking.AddDays(1), tracking, 500m, txns));
    }
}
