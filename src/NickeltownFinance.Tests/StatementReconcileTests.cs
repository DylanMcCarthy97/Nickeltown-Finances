using NickeltownFinance.Core.Ledger;
using Xunit;

namespace NickeltownFinance.Tests;

public class StatementReconcileTests
{
    [Fact]
    public void Compare_MatchesWhenOpeningAndClosingEqualAnz()
    {
        var result = StatementReconcile.Compare(
            ledgerOpening: 1000m,
            ledgerClosing: 1250m,
            ledgerIncome: 400m,
            ledgerExpenses: 150m,
            ledgerCount: 8,
            statementOpening: 1000m,
            statementClosing: 1250m,
            importedIncome: 400m,
            importedExpenses: 150m,
            importedCount: 8,
            fileName: "anz-sep.csv");

        Assert.True(result.IsFullyReconciled);
        Assert.Equal("Matched", result.State);
        Assert.True(result.OpeningMatches);
        Assert.True(result.ClosingMatches);
        Assert.Contains("anz-sep.csv", result.Summary);
    }

    [Fact]
    public void Compare_ReportsOpeningDifference()
    {
        var result = StatementReconcile.Compare(
            1012.50m, 1250m, 400m, 162.50m, 8,
            statementOpening: 1000m,
            statementClosing: 1250m,
            importedIncome: null,
            importedExpenses: null,
            importedCount: null);

        Assert.False(result.IsFullyReconciled);
        Assert.Equal("Mismatch", result.State);
        Assert.False(result.OpeningMatches);
        Assert.True(result.ClosingMatches);
        Assert.Equal(12.50m, result.OpeningDifference);
        Assert.Contains("higher than ANZ", result.OpeningStatus);
    }

    [Fact]
    public void Compare_NeedsBalancesWhenAnzFiguresMissing()
    {
        var result = StatementReconcile.Compare(
            1000m, 1250m, 400m, 150m, 8,
            statementOpening: null,
            statementClosing: null,
            importedIncome: 400m,
            importedExpenses: 150m,
            importedCount: 8);

        Assert.False(result.IsFullyReconciled);
        Assert.Equal("NeedsBalances", result.State);
        Assert.Contains("Type the opening and closing", result.Summary);
    }

    [Fact]
    public void InferOpening_SubtractsFirstTransactionFromRunningBalance()
    {
        var opening = StatementReconcile.InferOpeningFromFirstRunningBalance(
            runningBalanceAfterFirst: 1100m,
            firstNet: 100m);

        Assert.Equal(1000m, opening);
    }
}
