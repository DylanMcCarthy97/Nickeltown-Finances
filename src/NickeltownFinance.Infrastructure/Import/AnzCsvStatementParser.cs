using System.Globalization;
using System.Text;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;

namespace NickeltownFinance.Infrastructure.Import;

/// <summary>
/// Parser for the real ANZ bank CSV export used by Nickeltown.
/// Format (no headers — first row is data):
///   30/06/2026,"0.09",CREDIT INTEREST PAID
/// Columns: Date, Amount (signed), Description [, optional ignored columns]
/// Positive amount = income, negative amount = expense.
/// </summary>
public static class AnzCsvStatementParser
{
    public static readonly AnzColumnMapping DefaultMapping = new()
    {
        DateIndex = 0,
        AmountIndex = 1,
        DescriptionIndex = 2
    };

    public static StatementParseResult Parse(string filePath) =>
        Parse(filePath, mapping: null);

    public static StatementParseResult Parse(string filePath, AnzColumnMapping? mapping)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Statement file not found.", filePath);

        var allLines = ReadAllNonEmptyLines(filePath);
        if (allLines.Count == 0)
            throw CreateFailure(filePath, "The CSV file is empty.", allLines);

        var sampleRows = allLines.Take(5).Select(SplitCsvLine).ToList();

        var effectiveMapping = mapping ?? DetectMapping(allLines, sampleRows, filePath);
        return ParseWithMapping(filePath, allLines, effectiveMapping);
    }

    public static bool TryParseAnzRow(IReadOnlyList<string> fields, AnzColumnMapping mapping, out ParsedStatementRow? row, out string? error)
    {
        row = null;
        error = null;

        if (fields.Count == 0)
        {
            error = "Empty row.";
            return false;
        }

        var dateText = GetField(fields, mapping.DateIndex);
        var amountText = GetField(fields, mapping.AmountIndex);
        var description = GetField(fields, mapping.DescriptionIndex);
        var reference = mapping.ReferenceIndex is int refIdx ? GetField(fields, refIdx) : string.Empty;

        if (string.IsNullOrWhiteSpace(dateText) && string.IsNullOrWhiteSpace(amountText) && string.IsNullOrWhiteSpace(description))
        {
            error = "Blank row.";
            return false;
        }

        if (!TryParseAnzDate(dateText, out var date))
        {
            error = $"Unreadable date '{dateText}'.";
            return false;
        }

        if (!TryParseAnzAmount(amountText, out var signedAmount))
        {
            error = $"Unreadable amount '{amountText}'.";
            return false;
        }

        if (signedAmount == 0)
        {
            error = "Zero amount.";
            return false;
        }

        var debit = signedAmount < 0 ? Math.Abs(signedAmount) : 0m;
        var credit = signedAmount > 0 ? signedAmount : 0m;
        var amount = Math.Abs(signedAmount);

        row = new ParsedStatementRow
        {
            Date = date.Date,
            Description = description.Trim(),
            Debit = debit,
            Credit = credit,
            Reference = reference.Trim(),
            Fingerprint = ImportFingerprint.Compute(date.Date, amount, description, reference)
        };
        return true;
    }

    private static AnzColumnMapping DetectMapping(
        IReadOnlyList<string> allLines,
        IReadOnlyList<IReadOnlyList<string>> sampleRows,
        string filePath)
    {
        // Primary: real ANZ export — no headers, Date | Amount | Description
        if (LooksLikeAnzDataRow(sampleRows[0], DefaultMapping))
        {
            // Confirm at least half of the first few rows parse as ANZ data
            var probe = sampleRows.Take(Math.Min(5, sampleRows.Count)).ToList();
            var ok = probe.Count(r => LooksLikeAnzDataRow(r, DefaultMapping));
            if (ok >= Math.Max(1, (probe.Count + 1) / 2))
                return DefaultMapping;
        }

        // Some exports include a header row; skip it if row 0 fails and row 1 succeeds
        if (sampleRows.Count >= 2 &&
            !LooksLikeAnzDataRow(sampleRows[0], DefaultMapping) &&
            LooksLikeAnzDataRow(sampleRows[1], DefaultMapping))
        {
            return new AnzColumnMapping
            {
                DateIndex = 0,
                AmountIndex = 1,
                DescriptionIndex = 2,
                SkipFirstRow = true
            };
        }

        // Last resort: scan for a 3-column layout that works on most rows
        var maxCols = sampleRows.Max(r => r.Count);
        for (var dateIdx = 0; dateIdx < Math.Min(maxCols, 4); dateIdx++)
        for (var amountIdx = 0; amountIdx < Math.Min(maxCols, 4); amountIdx++)
        for (var descIdx = 0; descIdx < Math.Min(maxCols, 4); descIdx++)
        {
            if (dateIdx == amountIdx || dateIdx == descIdx || amountIdx == descIdx)
                continue;

            var candidate = new AnzColumnMapping
            {
                DateIndex = dateIdx,
                AmountIndex = amountIdx,
                DescriptionIndex = descIdx
            };

            var probe = sampleRows.Take(Math.Min(5, sampleRows.Count)).ToList();
            var ok = probe.Count(r => LooksLikeAnzDataRow(r, candidate));
            if (ok >= Math.Max(1, (probe.Count + 1) / 2))
                return candidate;
        }

        throw CreateFailure(
            filePath,
            "This file does not match the ANZ bank export format.\n\n" +
            "Nickeltown Finance expects an ANZ CSV with no headers, where each row is:\n" +
            "  Date (dd/MM/yyyy), Amount, Description\n\n" +
            "Example:\n" +
            "  30/06/2026,\"0.09\",CREDIT INTEREST PAID\n\n" +
            "Positive amounts are income; negative amounts are expenses.\n" +
            "You can map columns manually below.",
            allLines);
    }

    private static StatementParseResult ParseWithMapping(
        string filePath,
        IReadOnlyList<string> allLines,
        AnzColumnMapping mapping)
    {
        var warnings = new List<string>();
        var rows = new List<ParsedStatementRow>();
        var start = mapping.SkipFirstRow ? 1 : 0;

        for (var i = start; i < allLines.Count; i++)
        {
            var fields = SplitCsvLine(allLines[i]);
            if (!TryParseAnzRow(fields, mapping, out var row, out var error))
            {
                if (error is not null && error is not "Blank row." and not "Zero amount.")
                    warnings.Add($"Row {i + 1}: {error}");
                continue;
            }

            rows.Add(row!);
        }

        if (rows.Count == 0)
        {
            throw CreateFailure(
                filePath,
                "No valid ANZ transactions were found using the current column mapping.\n\n" +
                "Expected: Date (dd/MM/yyyy), Amount, Description.\n" +
                "Example: 30/06/2026,\"0.09\",CREDIT INTEREST PAID",
                allLines);
        }

        return new StatementParseResult
        {
            Format = StatementFormat.Csv,
            BankName = "ANZ",
            FileName = Path.GetFileName(filePath),
            Rows = rows,
            Warnings = warnings
        };
    }

    private static bool LooksLikeAnzDataRow(IReadOnlyList<string> fields, AnzColumnMapping mapping)
    {
        if (fields.Count <= Math.Max(mapping.DateIndex, Math.Max(mapping.AmountIndex, mapping.DescriptionIndex)))
            return false;

        return TryParseAnzDate(GetField(fields, mapping.DateIndex), out _) &&
               TryParseAnzAmount(GetField(fields, mapping.AmountIndex), out var amount) &&
               amount != 0;
    }

    private static AnzCsvParseException CreateFailure(string filePath, string reason, IReadOnlyList<string> allLines)
    {
        var sampleLines = allLines.Take(5).ToList();
        var sampleRows = sampleLines.Select(SplitCsvLine).Cast<IReadOnlyList<string>>().ToList();
        var colCount = sampleRows.Count > 0 ? sampleRows.Max(r => r.Count) : 0;

        return new AnzCsvParseException(reason, new AnzParseFailure
        {
            Reason = reason,
            SampleLines = sampleLines,
            SampleRows = sampleRows,
            DetectedColumnCount = colCount,
            FileName = Path.GetFileName(filePath)
        });
    }

    public static bool TryParseAnzDate(string text, out DateTime date)
    {
        text = (text ?? string.Empty).Trim().Trim('"', '\'');
        var formats = new[]
        {
            "d/M/yyyy", "dd/MM/yyyy", "d/M/yy", "dd/MM/yy"
        };

        if (DateTime.TryParseExact(text, formats, CultureInfo.GetCultureInfo("en-AU"),
                DateTimeStyles.None, out date))
            return true;

        return DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);
    }

    public static bool TryParseAnzAmount(string text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        text = text.Trim().Trim('"', '\'')
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace("AUD", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        var negative = false;
        if (text.StartsWith('(') && text.EndsWith(')'))
        {
            negative = true;
            text = text[1..^1].Trim();
        }

        // ANZ amounts use a period decimal separator, e.g. "0.09" or "-12.50"
        if (!decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out value) &&
            !decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowLeadingSign,
                CultureInfo.GetCultureInfo("en-AU"), out value))
            return false;

        if (negative) value = -Math.Abs(value);
        return true;
    }

    private static string GetField(IReadOnlyList<string> fields, int index) =>
        index >= 0 && index < fields.Count ? fields[index].Trim() : string.Empty;

    private static List<string> ReadAllNonEmptyLines(string filePath)
    {
        var lines = new List<string>();
        using var reader = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (!string.IsNullOrWhiteSpace(line))
                lines.Add(line.TrimStart('\uFEFF'));
        }

        return lines;
    }

    public static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result;
    }
}

public sealed class AnzCsvParseException : Exception
{
    public AnzParseFailure Failure { get; }

    public AnzCsvParseException(string message, AnzParseFailure failure)
        : base(message)
    {
        Failure = failure;
    }
}
