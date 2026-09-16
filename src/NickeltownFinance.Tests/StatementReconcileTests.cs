using NickeltownFinance.Core.DTOs;
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
    public void Compare_SameOpeningAndClosingGap_IsStartingBalance()
    {
        var result = StatementReconcile.Compare(
            ledgerOpening: 1012.50m,
            ledgerClosing: 1262.50m,
            ledgerIncome: 400m,
            ledgerExpenses: 150m,
            ledgerCount: 8,
            statementOpening: 1000m,
            statementClosing: 1250m,
            importedIncome: 400m,
            importedExpenses: 150m,
            importedCount: 8);

        Assert.False(result.IsFullyReconciled);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("Starting bank balance is off", issue.Title);
        Assert.Contains("same amount", issue.Why, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Edit starting balance", issue.HowToFix);
    }

    [Fact]
    public void Compare_ListsExtraLedgerRows_AndTellsHowToFix()
    {
        StatementReconcileLine[] extra =
        [
            new()
            {
                Date = new DateTime(2026, 9, 10),
                Description = "MANUAL DUPLICATE",
                MoneyOut = 40m
            }
        ];

        var result = StatementReconcile.Compare(
            1000m, 1210m, 400m, 190m, 9,
            statementOpening: 1000m,
            statementClosing: 1250m,
            importedIncome: 400m,
            importedExpenses: 150m,
            importedCount: 8,
            fileName: "anz-sep.csv",
            extraLedgerTransactions: extra);

        Assert.Contains(result.Issues, i => i.Title.Contains("not on this ANZ import"));
        Assert.Contains(result.Issues, i => i.HowToFix.Contains("delete", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("MANUAL DUPLICATE", Assert.Single(result.ExtraLedgerTransactions).Description);
    }

    [Fact]
    public void DiffTransactions_MatchesOnDateAmountAndDescription()
    {
        StatementReconcileLine[] ledger =
        [
            new() { Date = new DateTime(2026, 8, 22), Description = "SQUARE", MoneyIn = 200m },
            new() { Date = new DateTime(2026, 9, 1), Description = "BUNNINGS", MoneyOut = 50m }
        ];
        StatementReconcileLine[] imported =
        [
            new() { Date = new DateTime(2026, 8, 22), Description = "SQUARE", MoneyIn = 200m }
        ];

        var (extra, missing) = StatementReconcile.DiffTransactions(ledger, imported);

        Assert.Equal("BUNNINGS", Assert.Single(extra).Description);
        Assert.Empty(missing);
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
