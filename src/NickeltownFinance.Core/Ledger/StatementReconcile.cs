using NickeltownFinance.Core.DTOs;

namespace NickeltownFinance.Core.Ledger;

/// <summary>
/// Compares treasurer-report ledger totals to an imported ANZ statement
/// and explains how to fix mismatches.
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

    public static (IReadOnlyList<StatementReconcileLine> ExtraLedger, IReadOnlyList<StatementReconcileLine> MissingImport)
        DiffTransactions(
            IReadOnlyList<StatementReconcileLine> ledger,
            IReadOnlyList<StatementReconcileLine> imported)
    {
        var remainingImport = imported.ToList();
        var extra = new List<StatementReconcileLine>();

        foreach (var row in ledger)
        {
            var match = remainingImport.FindIndex(i => SameRow(i, row));
            if (match >= 0)
                remainingImport.RemoveAt(match);
            else
                extra.Add(row);
        }

        return (extra, remainingImport);
    }

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
        string? fileName = null,
        IReadOnlyList<StatementReconcileLine>? extraLedgerTransactions = null,
        IReadOnlyList<StatementReconcileLine>? missingImportTransactions = null)
    {
        extraLedgerTransactions ??= [];
        missingImportTransactions ??= [];

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
            ImportedTransactionCount = importedCount,
            ExtraLedgerTransactions = extraLedgerTransactions,
            MissingImportTransactions = missingImportTransactions
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
            result.IncomeMatches = extraLedgerTransactions.Count == 0 && missingImportTransactions.Count == 0;
            result.ExpensesMatch = result.IncomeMatches;
        }

        if (importedCount is { } count)
        {
            result.CountsMatch = ledgerCount == count && extraLedgerTransactions.Count == 0 && missingImportTransactions.Count == 0;
            result.HasImportTotals = true;
        }
        else
        {
            result.CountsMatch = extraLedgerTransactions.Count == 0 && missingImportTransactions.Count == 0;
        }

        result.IsFullyReconciled =
            result.HasStatementOpening &&
            result.HasStatementClosing &&
            result.OpeningMatches &&
            result.ClosingMatches &&
            result.IncomeMatches &&
            result.ExpensesMatch &&
            result.CountsMatch &&
            extraLedgerTransactions.Count == 0 &&
            missingImportTransactions.Count == 0;

        result.Issues = BuildIssues(result);
        result.State = ResolveState(result);
        result.Summary = BuildSummary(result);
        result.OpeningStatus = Describe(result.HasStatementOpening, result.OpeningMatches, result.OpeningDifference);
        result.ClosingStatus = Describe(result.HasStatementClosing, result.ClosingMatches, result.ClosingDifference);
        return result;
    }

    private static bool SameRow(StatementReconcileLine left, StatementReconcileLine right) =>
        left.Date.Date == right.Date.Date &&
        MoneyEquals(left.MoneyIn, right.MoneyIn) &&
        MoneyEquals(left.MoneyOut, right.MoneyOut) &&
        string.Equals(left.Description.Trim(), right.Description.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string ResolveState(StatementReconcileResult result)
    {
        if (result.IsFullyReconciled)
            return "Matched";
        if (!result.HasStatementOpening || !result.HasStatementClosing)
            return "NeedsBalances";
        return "Mismatch";
    }

    private static IReadOnlyList<StatementReconcileIssue> BuildIssues(StatementReconcileResult result)
    {
        if (result.IsFullyReconciled || !result.HasStatementOpening || !result.HasStatementClosing)
            return [];

        var issues = new List<StatementReconcileIssue>();
        var carryForwardOff = result.OpeningMatches == false &&
                              result.ClosingMatches == false &&
                              MoneyEquals(result.OpeningDifference, result.ClosingDifference);
        var extraNet = result.ExtraLedgerTransactions.Sum(t => t.Net);
        var missingNet = result.MissingImportTransactions.Sum(t => t.Net);

        if (carryForwardOff)
        {
            var adjust = decimal.Round(-result.OpeningDifference, 2, MidpointRounding.AwayFromZero);
            issues.Add(new StatementReconcileIssue
            {
                Title = "Starting bank balance is off",
                Why =
                    $"Opening is {FormatDelta(result.OpeningDifference)} and closing is out by the same amount. " +
                    "This statement's transactions are fine — the difference was already in the ledger before this period.",
                HowToFix =
                    $"Go to Financial years → Edit starting balance and change the starting bank figure by {adjust:C} " +
                    "(lower it if the ledger is higher than ANZ, raise it if the ledger is lower). " +
                    "Then re-import any missing ANZ activity from before this statement."
            });
        }
        else
        {
            if (!result.OpeningMatches)
            {
                issues.Add(new StatementReconcileIssue
                {
                    Title = "Opening does not match ANZ",
                    Why =
                        $"The ledger opening is {FormatDelta(result.OpeningDifference)}. " +
                        "That is the bank total at the start of the From date, which should equal the ANZ opening.",
                    HowToFix =
                        "Confirm the From date is the statement start (usually the 22nd). " +
                        "If the date is right, the club starting bank balance or an earlier import is wrong — " +
                        "use Financial years → Edit starting balance, or import the previous ANZ statement."
                });
            }

            if (!result.ClosingMatches)
            {
                var why = result.OpeningMatches
                    ? $"Closing is {FormatDelta(result.ClosingDifference)}. Opening matches, so the difference is in this period's transactions."
                    : $"Closing is {FormatDelta(result.ClosingDifference)}, and that is not the same gap as opening, so this period's income/expenses also differ from ANZ.";
                issues.Add(new StatementReconcileIssue
                {
                    Title = "Closing does not match ANZ",
                    Why = why,
                    HowToFix = result.HasTransactionDiffs
                        ? "Fix the extra or missing transactions listed below, then check balances again."
                        : "Compare the bank transactions on this report to the ANZ PDF. Import any missing rows, or delete duplicates."
                });
            }
        }

        if (result.ExtraLedgerTransactions.Count > 0)
        {
            issues.Add(new StatementReconcileIssue
            {
                Title = $"{result.ExtraLedgerTransactions.Count} ledger transaction(s) are not on this ANZ import",
                Why =
                    $"These rows are in Nickeltown Finance for the report dates but not in {FileLabel(result)}. " +
                    $"Together they move the ledger by {extraNet:C}.",
                HowToFix =
                    "Open Transactions and delete them if they were typed in twice. " +
                    "Keep them only if they are real and not on this ANZ statement — then closing will not match ANZ."
            });
        }

        if (result.MissingImportTransactions.Count > 0)
        {
            issues.Add(new StatementReconcileIssue
            {
                Title = $"{result.MissingImportTransactions.Count} ANZ row(s) are missing from this report",
                Why =
                    $"These rows are in {FileLabel(result)} but not in the report date range. " +
                    $"Together they move ANZ by {missingNet:C}.",
                HowToFix =
                    "Click Use this statement so the report dates match the import, or import the CSV again if the rows were skipped."
            });
        }

        if (!result.HasTransactionDiffs && result.OpeningMatches && (!result.IncomeMatches || !result.ExpensesMatch))
        {
            issues.Add(new StatementReconcileIssue
            {
                Title = "Income or expenses do not match the ANZ import",
                Why = "The number of rows matches, but money in or money out is different. A amount may have been edited after import.",
                HowToFix = "Find the edited transaction on the Transactions page and restore the ANZ amount, or undo and re-import the statement."
            });
        }

        return issues;
    }

    private static string BuildSummary(StatementReconcileResult result)
    {
        if (result.IsFullyReconciled)
            return string.IsNullOrWhiteSpace(result.FileName)
                ? "Reconciled — opening, closing, and totals match the ANZ statement."
                : $"Reconciled to {result.FileName} — opening, closing, and transactions match.";

        if (!result.HasStatementOpening || !result.HasStatementClosing)
            return "Type the opening and closing from the ANZ statement (the figures at the top and bottom of the PDF).";

        if (result.Issues.Count > 0)
        {
            var first = result.Issues[0];
            return $"{first.Why} {first.HowToFix}";
        }

        return "Check the ANZ statement figures and try again.";
    }

    private static string FileLabel(StatementReconcileResult result) =>
        string.IsNullOrWhiteSpace(result.FileName) ? "the ANZ import" : result.FileName;

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
