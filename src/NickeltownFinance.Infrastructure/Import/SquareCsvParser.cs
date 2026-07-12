using System.Globalization;
using System.Text;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;

namespace NickeltownFinance.Infrastructure.Import;

/// <summary>
/// Parses Square transaction CSV exports and groups rows into deposits.
/// Supports common Square column names (Deposit ID, Net Total, Fees, etc.).
/// </summary>
public static class SquareCsvParser
{
    public static SquareImportPreview Parse(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Square file not found.", filePath);

        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        if (lines.Length == 0)
            throw new InvalidOperationException("The Square file is empty.");

        var headerIndex = FindHeaderRow(lines);
        if (headerIndex < 0)
            throw new InvalidOperationException(
                "Could not find Square column headers. Export transactions from Square Dashboard as CSV.");

        var headers = SplitCsvLine(lines[headerIndex])
            .Select(h => h.Trim().Trim('"'))
            .ToList();

        var map = BuildColumnMap(headers);
        if (map.NetIndex < 0 && map.TotalCollectedIndex < 0 && map.GrossSalesIndex < 0)
            throw new InvalidOperationException(
                "Square CSV must include Net Total / Net Amount or Gross Sales / Total Collected columns.");

        var warnings = new List<string>();
        var linesByDeposit = new Dictionary<string, List<SquareTransactionPreviewLine>>(StringComparer.OrdinalIgnoreCase);

        for (var i = headerIndex + 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            var cols = SplitCsvLine(lines[i]);
            if (cols.Count == 0)
                continue;

            try
            {
                var line = ParseLine(cols, map);
                if (line is null)
                    continue;

                var depositKey = string.IsNullOrWhiteSpace(line.ExternalDepositId)
                    ? $"DATE:{line.DepositDate:yyyy-MM-dd}|NET:{line.NetAmount:0.00}"
                    : line.ExternalDepositId;

                if (!linesByDeposit.TryGetValue(depositKey, out var group))
                {
                    group = [];
                    linesByDeposit[depositKey] = group;
                }

                group.Add(line);
            }
            catch (Exception ex)
            {
                warnings.Add($"Row {i + 1}: {ex.Message}");
            }
        }

        if (linesByDeposit.Count == 0)
            throw new InvalidOperationException("No Square transactions were found in the file.");

        var deposits = new List<SquarePreviewRow>();
        foreach (var (key, group) in linesByDeposit)
        {
            var depositDate = group.Select(g => g.DepositDate ?? g.Date).DefaultIfEmpty(group[0].Date).Max();
            var depositId = group.Select(g => g.ExternalDepositId).FirstOrDefault(id => !string.IsNullOrWhiteSpace(id))
                            ?? key;
            var gross = group.Sum(g => g.GrossAmount);
            var fees = group.Sum(g => g.Fees);
            var net = group.Sum(g => g.NetAmount);
            if (net == 0 && gross != 0)
                net = gross - fees;

            var fingerprint = ImportFingerprint.Compute(depositDate, net, depositId, "SQUARE");

            deposits.Add(new SquarePreviewRow
            {
                IsSelected = true,
                DepositId = depositId,
                DepositDate = depositDate.Date,
                GrossAmount = gross,
                Fees = fees,
                NetAmount = net,
                TransactionCount = group.Count,
                Status = ImportRowStatus.New,
                Fingerprint = fingerprint,
                Lines = group
            });
        }

        return new SquareImportPreview
        {
            FileName = Path.GetFileName(filePath),
            Deposits = deposits.OrderByDescending(d => d.DepositDate).ThenBy(d => d.DepositId).ToList(),
            Warnings = warnings
        };
    }

    private static int FindHeaderRow(string[] lines)
    {
        for (var i = 0; i < Math.Min(lines.Length, 10); i++)
        {
            var cols = SplitCsvLine(lines[i]).Select(c => c.Trim().Trim('"').ToUpperInvariant()).ToList();
            if (cols.Any(c => c.Contains("DEPOSIT") || c.Contains("NET TOTAL") || c.Contains("GROSS SALES") ||
                              c is "DATE" or "TIME" or "AMOUNT" or "FEES"))
                return i;
        }

        return lines.Length > 0 ? 0 : -1;
    }

    private sealed class ColumnMap
    {
        public int DateIndex = -1;
        public int TimeIndex = -1;
        public int DepositDateIndex = -1;
        public int DepositIdIndex = -1;
        public int GrossSalesIndex = -1;
        public int TotalCollectedIndex = -1;
        public int FeesIndex = -1;
        public int NetIndex = -1;
        public int CustomerIndex = -1;
        public int DescriptionIndex = -1;
        public int LocationIndex = -1;
        public int TenderNoteIndex = -1;
        public int CategoryIndex = -1;
        public int PaymentMethodIndex = -1;
        public int TransactionIdIndex = -1;
        public int PaymentIdIndex = -1;
    }

    private static ColumnMap BuildColumnMap(IReadOnlyList<string> headers)
    {
        var map = new ColumnMap();
        for (var i = 0; i < headers.Count; i++)
        {
            var h = headers[i].Trim().ToUpperInvariant();
            if (h is "DATE" or "TRANSACTION DATE" or "CREATED AT")
                map.DateIndex = map.DateIndex < 0 ? i : map.DateIndex;
            else if (h is "TIME")
                map.TimeIndex = i;
            else if (h is "DEPOSIT DATE" or "PAYOUT DATE")
                map.DepositDateIndex = i;
            else if (h is "DEPOSIT ID" or "PAYOUT ID" or "DEPOSITID")
                map.DepositIdIndex = i;
            else if (h is "TOTAL COLLECTED")
                map.TotalCollectedIndex = i;
            else if (h is "GROSS SALES" or "GROSS AMOUNT")
                map.GrossSalesIndex = map.GrossSalesIndex < 0 ? i : map.GrossSalesIndex;
            else if (h is "AMOUNT" or "TOTAL")
                map.GrossSalesIndex = map.GrossSalesIndex < 0 ? i : map.GrossSalesIndex;
            else if (h is "FEES" or "FEE" or "PROCESSING FEES")
                map.FeesIndex = i;
            else if (h is "NET TOTAL" or "NET AMOUNT" or "NET" or "PAYOUT AMOUNT")
                map.NetIndex = i;
            else if (h is "CUSTOMER NAME" or "CUSTOMER" or "BUYER NAME")
                map.CustomerIndex = i;
            else if (h is "TENDER NOTE")
                map.TenderNoteIndex = i;
            else if (h is "DESCRIPTION" or "DETAILS" or "ITEM" or "TRANSACTION TYPE")
                map.DescriptionIndex = map.DescriptionIndex < 0 ? i : map.DescriptionIndex;
            else if (h is "LOCATION")
                map.LocationIndex = i;
            else if (h is "CATEGORY" or "ITEM CATEGORY")
                map.CategoryIndex = i;
            else if (h is "PAYMENT METHOD" or "CARD BRAND" or "SOURCE")
                map.PaymentMethodIndex = map.PaymentMethodIndex < 0 ? i : map.PaymentMethodIndex;
            else if (h is "TRANSACTION ID")
                map.TransactionIdIndex = i;
            else if (h is "PAYMENT ID" or "PAYMENTID")
                map.PaymentIdIndex = i;
        }

        return map;
    }

    private static SquareTransactionPreviewLine? ParseLine(IReadOnlyList<string> cols, ColumnMap map)
    {
        string Get(int index) =>
            index >= 0 && index < cols.Count ? cols[index].Trim().Trim('"') : string.Empty;

        var dateText = Get(map.DateIndex);
        if (string.IsNullOrWhiteSpace(dateText) && map.DepositDateIndex >= 0)
            dateText = Get(map.DepositDateIndex);

        if (!TryParseDate(dateText, out var date))
            return null;

        var timeText = Get(map.TimeIndex);
        var transactionTime = date.Date;
        if (!string.IsNullOrWhiteSpace(timeText) &&
            TimeSpan.TryParse(timeText, CultureInfo.InvariantCulture, out var timeOfDay))
        {
            transactionTime = date.Date.Add(timeOfDay);
        }

        DateTime? depositDate = null;
        var depositDateText = Get(map.DepositDateIndex);
        if (TryParseDate(depositDateText, out var dd))
            depositDate = dd;

        var gross = map.TotalCollectedIndex >= 0
            ? ParseMoney(Get(map.TotalCollectedIndex))
            : map.GrossSalesIndex >= 0
                ? ParseMoney(Get(map.GrossSalesIndex))
                : 0m;
        var fees = Math.Abs(ParseMoney(Get(map.FeesIndex)));
        var net = ParseMoney(Get(map.NetIndex));
        if (net == 0 && gross != 0)
            net = gross - fees;

        // Skip empty / zero rows and non-payment status rows with no money movement.
        if (gross == 0 && net == 0 && fees == 0)
            return null;

        var depositId = Get(map.DepositIdIndex);
        var externalTxnId = Get(map.TransactionIdIndex);
        var paymentId = Get(map.PaymentIdIndex);
        var rawDescription = Get(map.DescriptionIndex);
        var tenderNote = Get(map.TenderNoteIndex);
        var customerColumn = Get(map.CustomerIndex);
        var location = Get(map.LocationIndex);
        var (description, customerName) = SquareDescriptionHelper.Parse(rawDescription, customerColumn);

        var fingerprint = ImportFingerprint.Compute(
            date,
            net != 0 ? net : gross,
            externalTxnId.Length > 0 ? externalTxnId : description,
            depositId);

        return new SquareTransactionPreviewLine
        {
            Date = date.Date,
            TransactionTime = transactionTime,
            DepositDate = depositDate?.Date,
            ExternalDepositId = depositId,
            CustomerName = customerName,
            Description = description,
            Location = location,
            PaymentId = paymentId,
            Category = Get(map.CategoryIndex),
            GrossAmount = gross,
            Fees = fees,
            NetAmount = net,
            PaymentMethod = Get(map.PaymentMethodIndex),
            ExternalTransactionId = externalTxnId,
            Fingerprint = fingerprint
        };
    }

    private static bool TryParseDate(string text, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var formats = new[]
        {
            "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "dd/MM/yyyy", "d/M/yyyy",
            "MM/dd/yyyy", "M/d/yyyy", "dd-MM-yyyy", "yyyy/MM/dd",
            "dd MMM yyyy", "d MMM yyyy", "MMM d, yyyy"
        };

        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out date))
            return true;

        return DateTime.TryParse(text, CultureInfo.GetCultureInfo("en-AU"),
                   DateTimeStyles.AllowWhiteSpaces, out date)
               || DateTime.TryParse(text, CultureInfo.InvariantCulture,
                   DateTimeStyles.AllowWhiteSpaces, out date);
    }

    private static decimal ParseMoney(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var cleaned = text.Replace("$", "", StringComparison.Ordinal)
            .Replace(",", "", StringComparison.Ordinal)
            .Replace("(", "-", StringComparison.Ordinal)
            .Replace(")", "", StringComparison.Ordinal)
            .Trim();

        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static List<string> SplitCsvLine(string line)
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
