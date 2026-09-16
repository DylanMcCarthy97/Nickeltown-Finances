using NickeltownFinance.Core.DTOs;

namespace NickeltownFinance.Core.Ledger;

/// <summary>
/// Compares treasurer-report ledger totals to an imported ANZ statement.
/// </summary>
public static class StatementReconcile
{
    public static bool MoneyEquals(decimal left, decimal right) =>
        decimal.Round(left, 2, MidpointRounding.AwayFromZero) ==
        decimal.Round(right, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// ANZ running balance is after the transaction, so opening is that balance minus the first row's net.
    /// </summary>
    public static decimal InferOpeningFromFirstRunningBalance(decimal runningBalanceAfterFirst, decimal firstNet) =>
        decimal.Round(runningBalanceAfterFirst - firstNet, 2, MidpointRounding.AwayFromZero);

    public static StatementReconcileResult Compare(
        decimal ledgerOpening,
        decimal ledgerClosing,
        decimal ledgerIncome,
        decimal ledgerExpenses,
        int ledgerCount,
        decimal? statementOpening,
        decimal? statementClosing,
        decimal? importedIncome,
        decimal? importedExpenses,
        int? importedCount,
        string? fileName = null)
    {
        var result = new StatementReconcileResult
        {
            FileName = fileName ?? string.Empty,
            LedgerOpening = ledgerOpening,
            LedgerClosing = ledgerClosing,
            LedgerIncome = ledgerIncome,
            LedgerExpenses = ledgerExpenses,
            LedgerTransactionCount = ledgerCount,
            StatementOpening = statementOpening,
            StatementClosing = statementClosing,
            ImportedIncome = importedIncome,
            ImportedExpenses = importedExpenses,
            ImportedTransactionCount = importedCount
        };

        if (statementOpening is { } opening)
        {
            result.OpeningDifference = decimal.Round(ledgerOpening - opening, 2, MidpointRounding.AwayFromZero);
            result.OpeningMatches = MoneyEquals(ledgerOpening, opening);
        }

        if (statementClosing is { } closing)
        {
            result.ClosingDifference = decimal.Round(ledgerClosing - closing, 2, MidpointRounding.AwayFromZero);
            result.ClosingMatches = MoneyEquals(ledgerClosing, closing);
        }

        if (importedIncome is { } income && importedExpenses is { } expenses)
        {
            result.IncomeMatches = MoneyEquals(ledgerIncome, income);
            result.ExpensesMatch = MoneyEquals(ledgerExpenses, expenses);
            result.HasImportTotals = true;
        }
        else
        {
            result.IncomeMatches = true;
            result.ExpensesMatch = true;
        }

        if (importedCount is { } count)
        {
            result.CountsMatch = ledgerCount == count;
            result.HasImportTotals = true;
        }
        else
        {
            result.CountsMatch = true;
        }

        result.IsFullyReconciled =
            result.HasStatementOpening &&
            result.HasStatementClosing &&
            result.OpeningMatches &&
            result.ClosingMatches &&
            result.IncomeMatches &&
            result.ExpensesMatch &&
            result.CountsMatch;

        result.State = ResolveState(result);
        result.Summary = BuildSummary(result);
        result.OpeningStatus = Describe(result.HasStatementOpening, result.OpeningMatches, result.OpeningDifference);
        result.ClosingStatus = Describe(result.HasStatementClosing, result.ClosingMatches, result.ClosingDifference);
        return result;
    }

    private static string ResolveState(StatementReconcileResult result)
    {
        if (result.IsFullyReconciled)
            return "Matched";
        if (!result.HasStatementOpening || !result.HasStatementClosing)
            return "NeedsBalances";
        if (!result.OpeningMatches || !result.ClosingMatches || !result.IncomeMatches || !result.ExpensesMatch || !result.CountsMatch)
            return "Mismatch";
        return "Partial";
    }

    private static string BuildSummary(StatementReconcileResult result)
    {
        if (result.IsFullyReconciled)
            return string.IsNullOrWhiteSpace(result.FileName)
                ? "Reconciled — opening, closing, and totals match the ANZ statement."
                : $"Reconciled to {result.FileName} — opening, closing, and transactions match.";

        if (!result.HasStatementOpening || !result.HasStatementClosing)
            return "Type the opening and closing from the ANZ statement (the figures at the top and bottom of the PDF).";

        if (!result.OpeningMatches && !result.ClosingMatches)
            return $"Opening is {FormatDelta(result.OpeningDifference)} and closing is {FormatDelta(result.ClosingDifference)} versus ANZ.";

        if (!result.OpeningMatches)
            return $"Opening is {FormatDelta(result.OpeningDifference)} versus the ANZ statement.";

        if (!result.ClosingMatches)
            return $"Closing is {FormatDelta(result.ClosingDifference)} versus the ANZ statement.";

        if (!result.CountsMatch)
            return $"Opening and closing match, but the ledger has {result.LedgerTransactionCount} transactions and the ANZ import has {result.ImportedTransactionCount}.";

        if (!result.IncomeMatches || !result.ExpensesMatch)
            return "Opening and closing match, but income or expenses do not match the imported ANZ totals.";

        return "Check the ANZ statement figures and try again.";
    }

    private static string Describe(bool hasValue, bool matches, decimal difference)
    {
        if (!hasValue)
            return "Enter ANZ figure";
        return matches ? "Matches" : FormatDelta(difference);
    }

    private static string FormatDelta(decimal difference)
    {
        var abs = Math.Abs(difference);
        if (difference > 0)
            return $"{abs:C} higher than ANZ";
        if (difference < 0)
            return $"{abs:C} lower than ANZ";
        return "Matches";
    }
}
