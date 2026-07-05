using System.Globalization;
using System.Text.RegularExpressions;
using LiteDB;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Interfaces;

namespace NickeltownFinance.Infrastructure.Services.ReceiptImport;

/// <summary>
/// Parses Australian receipt OCR text into structured fields with confidence scores.
/// </summary>
public sealed class ReceiptOcrFieldParser : IReceiptOcrFieldParser
{
    private static readonly Regex AbnRegex = new(@"\bABN[:\s]*(\d{2}\s?\d{3}\s?\d{3}\s?\d{3})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex InvoiceRegex = new(@"\b(?:TAX\s+INVOICE|INVOICE|INV|RECEIPT|REF|TAX\s+INV)[#:\s-]*([A-Z0-9-]{4,})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DateRegex = new(@"\b(\d{1,2}[/-]\d{1,2}[/-]\d{2,4})\b", RegexOptions.Compiled);
    private static readonly Regex MoneyRegex = new(@"(?i)(SUB\s*TOTAL|SUBTOTAL|GST|TOTAL|AMOUNT\s+DUE|BALANCE\s+DUE|GRAND\s+TOTAL)[^\d\n\r$]*\$?\s*([\d,]+\.\d{2})", RegexOptions.Compiled);
    private static readonly Regex PaymentRegex = new(@"\b(VISA|MASTERCARD|AMEX|EFTPOS|CASH|DEBIT|CREDIT|PAYPAL|AFTERPAY|ZIP)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public OcrExtractionResult Parse(string fullText, IReadOnlyDictionary<string, byte>? lineConfidences = null)
    {
        var text = fullText?.Trim() ?? string.Empty;
        var result = new OcrExtractionResult { FullText = text };

        if (string.IsNullOrWhiteSpace(text))
            return result;

        result.Supplier = ExtractSupplier(text);
        result.SupplierConfidence = ConfidenceForLine(result.Supplier, lineConfidences, fallback: 75);

        result.Abn = ExtractAbn(text);
        result.AbnConfidence = result.Abn is null ? null : (byte)92;

        result.InvoiceNumber = ExtractInvoiceNumber(text);
        result.InvoiceNumberConfidence = result.InvoiceNumber is null ? null : (byte)80;

        result.Date = ExtractDate(text);
        result.DateConfidence = result.Date is null ? null : (byte)85;

        ExtractAmounts(text, result);
        result.PaymentMethod = ExtractPaymentMethod(text);
        result.PaymentMethodConfidence = result.PaymentMethod is null ? null : (byte)78;

        result.Currency = text.Contains("AUD", StringComparison.OrdinalIgnoreCase) || text.Contains('$')
            ? "AUD"
            : null;
        result.CurrencyConfidence = result.Currency is null ? null : (byte)90;

        return result;
    }

    private static string? ExtractSupplier(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines.Take(8))
        {
            if (line.Length < 3 || line.Length > 60) continue;
            if (Regex.IsMatch(line, @"^\d", RegexOptions.None)) continue;
            if (line.Contains("ABN", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.Contains("TAX INVOICE", StringComparison.OrdinalIgnoreCase)) continue;
            return line.Trim();
        }

        return lines.FirstOrDefault()?.Trim();
    }

    private static string? ExtractAbn(string text)
    {
        var match = AbnRegex.Match(text);
        if (!match.Success) return null;
        return new string(match.Groups[1].Value.Where(char.IsDigit).ToArray());
    }

    private static string? ExtractInvoiceNumber(string text)
    {
        var match = InvoiceRegex.Match(text);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static DateTime? ExtractDate(string text)
    {
        foreach (Match match in DateRegex.Matches(text))
        {
            var raw = match.Groups[1].Value.Replace('-', '/');
            if (DateTime.TryParseExact(raw, ["d/M/yyyy", "dd/MM/yyyy", "d/M/yy", "dd/MM/yy"],
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;
        }

        return null;
    }

    private static void ExtractAmounts(string text, OcrExtractionResult result)
    {
        decimal? subtotal = null, gst = null, total = null;
        byte? subConf = null, gstConf = null, totalConf = null;

        foreach (Match match in MoneyRegex.Matches(text))
        {
            if (!decimal.TryParse(match.Groups[2].Value.Replace(",", ""), NumberStyles.Number,
                    CultureInfo.InvariantCulture, out var amount))
                continue;

            var label = match.Groups[1].Value.ToUpperInvariant();
            if (label.Contains("SUB"))
            {
                subtotal = amount;
                subConf = 82;
            }
            else if (label.Contains("GST"))
            {
                gst = amount;
                gstConf = 80;
            }
            else if (label.Contains("TOTAL") || label.Contains("DUE") || label.Contains("BALANCE"))
            {
                total = amount;
                totalConf = 88;
            }
        }

        if (total is null)
        {
            var amounts = Regex.Matches(text, @"\$?\s*([\d,]+\.\d{2})")
                .Select(m => decimal.TryParse(m.Groups[1].Value.Replace(",", ""), NumberStyles.Number,
                    CultureInfo.InvariantCulture, out var v) ? v : (decimal?)null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();
            if (amounts.Count > 0)
            {
                total = amounts.Max();
                totalConf = 70;
            }
        }

        result.Subtotal = subtotal;
        result.SubtotalConfidence = subConf;
        result.Gst = gst;
        result.GstConfidence = gstConf;
        result.Total = total;
        result.TotalConfidence = totalConf;
    }

    private static string? ExtractPaymentMethod(string text)
    {
        var match = PaymentRegex.Match(text);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }

    private static byte ConfidenceForLine(string? value, IReadOnlyDictionary<string, byte>? lineConfidences, byte fallback)
    {
        if (string.IsNullOrWhiteSpace(value) || lineConfidences is null) return fallback;
        foreach (var pair in lineConfidences)
        {
            if (pair.Key.Contains(value, StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        }

        return fallback;
    }
}
