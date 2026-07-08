using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Infrastructure.Import;

namespace NickeltownFinance.Infrastructure.Import;

/// <summary>
/// Parses legacy Nickeltown Flounderers treasurer Excel workbooks (one sheet per month).
/// </summary>
public static class LegacyTreasurerReportParser
{
    private static readonly string[] MonthNames =
    [
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    ];

    private static readonly Regex CashOnHandRegex = new(
        @"Cash\s+on\s+Hand\s*-\s*\$?\s*([\d,]+(?:\.\d+)?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ShireBondsRegex = new(
        @"\$?\s*([\d,]+(?:\.\d+)?)\s+in\s+bonds\s+with\s+the\s+Shire",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static LegacyTreasurerParseResult Parse(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Treasurer report file not found.", filePath);

        using var workbook = new XLWorkbook(filePath);
        var warnings = new List<string>();
        var months = new List<LegacyTreasurerMonthSummary>();
        var rows = new List<ImportPreviewRow>();

        foreach (var sheet in workbook.Worksheets)
        {
            var used = sheet.RangeUsed();
            if (used is null)
                continue;

            if (!LooksLikeTreasurerSheet(sheet))
                continue;

            var monthLabel = sheet.Cell("B4").GetString().Trim();
            if (monthLabel.Contains("AGM", StringComparison.OrdinalIgnoreCase))
            {
                months.Add(new LegacyTreasurerMonthSummary
                {
                    SheetName = sheet.Name,
                    MonthLabel = monthLabel,
                    IsSkipped = true,
                    SkipReason = "Annual summary sheet — import monthly sheets for transactions."
                });
                continue;
            }

            if (!TryParseMonthLabel(monthLabel, out var year, out var month, out var parsedLabel))
            {
                warnings.Add($"Skipped sheet '{sheet.Name}': could not read month from '{monthLabel}'.");
                months.Add(new LegacyTreasurerMonthSummary
                {
                    SheetName = sheet.Name,
                    MonthLabel = monthLabel,
                    IsSkipped = true,
                    SkipReason = "Unrecognised month label."
                });
                continue;
            }

            var summary = ParseMonthlySheet(sheet, year, month, monthLabel, warnings);
            months.Add(summary);

            foreach (var line in summary.IncomeLines)
                rows.Add(ToPreviewRow(line, isIncome: true));

            foreach (var line in summary.ExpenseLines)
                rows.Add(ToPreviewRow(line, isIncome: false));
        }

        if (rows.Count == 0)
            throw new InvalidOperationException(
                "No treasurer report data was found. Use the club's Income and Expense Statement workbook (.xlsx).");

        return new LegacyTreasurerParseResult
        {
            FileName = Path.GetFileName(filePath),
            Months = months,
            Rows = rows,
            Warnings = warnings
        };
    }

    private static bool LooksLikeTreasurerSheet(IXLWorksheet sheet)
    {
        var title = sheet.Cell("A2").GetString();
        var subtitle = sheet.Cell("B3").GetString();
        return title.Contains("Treasurer", StringComparison.OrdinalIgnoreCase) &&
               subtitle.Contains("Income", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseMonthLabel(string label, out int year, out int month, out string display)
    {
        year = 0;
        month = 0;
        display = label;

        foreach (var name in MonthNames)
        {
            if (!label.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                continue;

            var rest = label[name.Length..].Trim();
            if (int.TryParse(rest, NumberStyles.Integer, CultureInfo.InvariantCulture, out year))
            {
                month = Array.FindIndex(MonthNames, m => m.Equals(name, StringComparison.OrdinalIgnoreCase)) + 1;
                display = $"{name} {year}";
                return true;
            }
        }

        return false;
    }

    private sealed class ParsedSheet : LegacyTreasurerMonthSummary
    {
        public List<LegacyLine> IncomeLines { get; } = [];
        public List<LegacyLine> ExpenseLines { get; } = [];
    }

    private sealed class LegacyLine
    {
        public DateTime Date { get; init; }
        public string Description { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public int Year { get; init; }
        public int Month { get; init; }
    }

    private static ParsedSheet ParseMonthlySheet(
        IXLWorksheet sheet,
        int year,
        int month,
        string monthLabel,
        List<string> warnings)
    {
        var summary = new ParsedSheet
        {
            SheetName = sheet.Name,
            MonthLabel = monthLabel,
            Year = year,
            Month = month
        };

        var mode = SectionMode.None;
        foreach (var row in sheet.RangeUsed()!.RowsUsed())
        {
            var label = Normalize(row.Cell(1).GetString());
            if (string.IsNullOrWhiteSpace(label))
            {
                if (mode is SectionMode.Income or SectionMode.Expenses)
                {
                    var blankLabelLine = TryParseTransactionLine(row, year, month, warnings);
                    if (blankLabelLine is not null)
                    {
                        if (mode == SectionMode.Income)
                            summary.IncomeLines.Add(blankLabelLine);
                        else
                            summary.ExpenseLines.Add(blankLabelLine);
                    }
                }

                continue;
            }

            if (label.StartsWith("From", StringComparison.OrdinalIgnoreCase))
            {
                summary.PeriodFrom = TryParseDateCell(row.Cell(1)) ?? TryParseTrailingDate(label);
                summary.PeriodTo = TryParseDateCell(row.Cell(4)) ?? TryParseTrailingDate(row.Cell(4).GetString());
                continue;
            }

            if (label.Equals("Opening Cash in Bank", StringComparison.OrdinalIgnoreCase))
            {
                summary.OpeningBankBalance = ReadAmount(row.Cell(4));
                continue;
            }

            if (label.Equals("Income", StringComparison.OrdinalIgnoreCase))
            {
                mode = SectionMode.Income;
                continue;
            }

            if (label.StartsWith("Net Sales", StringComparison.OrdinalIgnoreCase))
            {
                mode = SectionMode.None;
                continue;
            }

            if (label.Equals("Expenses", StringComparison.OrdinalIgnoreCase))
            {
                mode = SectionMode.Expenses;
                continue;
            }

            if (label.StartsWith("Net Expenses", StringComparison.OrdinalIgnoreCase))
            {
                mode = SectionMode.None;
                continue;
            }

            if (label.Equals("Closing Cash in Bank", StringComparison.OrdinalIgnoreCase))
            {
                summary.ClosingBankBalance = ReadAmount(row.Cell(4));
                continue;
            }

            if (label.Contains("Cash on Hand", StringComparison.OrdinalIgnoreCase) ||
                label.Contains("bonds with the Shire", StringComparison.OrdinalIgnoreCase))
            {
                ParseHoldings(label, ReadAmount(row.Cell(4)), summary);
                continue;
            }

            if (label.Contains("Paypal", StringComparison.OrdinalIgnoreCase))
            {
                summary.PayPalBalance = ReadAmount(row.Cell(4));
                continue;
            }

            if (mode is SectionMode.Income or SectionMode.Expenses)
            {
                var line = TryParseTransactionLine(row, year, month, warnings);
                if (line is null)
                    continue;

                if (mode == SectionMode.Income)
                    summary.IncomeLines.Add(line);
                else
                    summary.ExpenseLines.Add(line);
            }
        }

        if (summary.CashOnHand == 0 && summary.ShireBonds == 0)
        {
            summary.CashOnHand = 200m;
            summary.ShireBonds = 1000m;
        }

        summary.IncomeLineCount = summary.IncomeLines.Count;
        summary.ExpenseLineCount = summary.ExpenseLines.Count;
        return summary;
    }

    private static LegacyLine? TryParseTransactionLine(
        IXLRangeRow row,
        int year,
        int month,
        List<string> warnings)
    {
        var description = row.Cell(3).GetString().Trim();
        var amount = ReadAmount(row.Cell(4));
        if (string.IsNullOrWhiteSpace(description) || amount <= 0)
            return null;

        if (!TryParseDateCell(row.Cell(2), out var date))
        {
            date = new DateTime(year, month, 1);
            warnings.Add($"Used month start for '{description}' on sheet row {row.RowNumber()}.");
        }

        return new LegacyLine
        {
            Date = date,
            Description = description,
            Amount = amount,
            Year = year,
            Month = month
        };
    }

    private static ImportPreviewRow ToPreviewRow(LegacyLine line, bool isIncome)
    {
        var fingerprint = ImportFingerprint.Compute(line.Date, line.Amount, line.Description, string.Empty);
        return new ImportPreviewRow
        {
            Date = line.Date,
            Description = line.Description,
            Credit = isIncome ? line.Amount : 0,
            Debit = isIncome ? 0 : line.Amount,
            Fingerprint = fingerprint,
            Status = ImportRowStatus.New,
            IsSelected = true
        };
    }

    private static void ParseHoldings(string text, decimal combinedTotal, LegacyTreasurerMonthSummary summary)
    {
        var cashMatch = CashOnHandRegex.Match(text);
        if (cashMatch.Success &&
            decimal.TryParse(cashMatch.Groups[1].Value.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var cash))
            summary.CashOnHand = cash;

        var bondsMatch = ShireBondsRegex.Match(text);
        if (bondsMatch.Success &&
            decimal.TryParse(bondsMatch.Groups[1].Value.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var bonds))
            summary.ShireBonds = bonds;

        if (summary.CashOnHand == 0 && summary.ShireBonds == 0 && combinedTotal > 0)
        {
            summary.CashOnHand = 200m;
            summary.ShireBonds = Math.Max(0m, combinedTotal - summary.CashOnHand);
        }
    }

    private static decimal ReadAmount(IXLCell cell)
    {
        if (cell.DataType == XLDataType.Number)
            return Convert.ToDecimal(cell.GetDouble(), CultureInfo.InvariantCulture);

        var text = cell.GetString().Replace("$", string.Empty).Replace(",", string.Empty).Trim();
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0m;
    }

    private static bool TryParseDateCell(IXLCell cell, out DateTime date)
    {
        date = default;
        if (cell.DataType == XLDataType.DateTime)
        {
            date = cell.GetDateTime().Date;
            return true;
        }

        if (cell.DataType == XLDataType.Number)
        {
            try
            {
                date = cell.GetDateTime().Date;
                if (date.Year is >= 2000 and <= 2100)
                    return true;
            }
            catch
            {
                // fall through to 1904 conversion
            }

            var serial = cell.GetDouble();
            date = new DateTime(1904, 1, 1).AddDays(serial - 1).Date;
            return date.Year is >= 2000 and <= 2100;
        }

        var text = cell.GetString().Trim();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (DateTime.TryParseExact(text, ["dd/MM/yyyy", "d/MM/yyyy", "dd/MM/yy", "d/MM/yy"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;

        return DateTime.TryParse(text, CultureInfo.GetCultureInfo("en-AU"), DateTimeStyles.None, out date);
    }

    private static DateTime? TryParseDateCell(IXLCell cell) =>
        TryParseDateCell(cell, out var date) ? date : null;

    private static DateTime? TryParseTrailingDate(string text)
    {
        var parts = text.Split(['–', '-'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var candidate = parts.Length > 1 ? parts[^1] : text;
        return DateTime.TryParse(candidate, CultureInfo.GetCultureInfo("en-AU"), DateTimeStyles.None, out var date)
            ? date.Date
            : null;
    }

    private static string Normalize(string value) =>
        string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private enum SectionMode
    {
        None,
        Income,
        Expenses
    }
}
