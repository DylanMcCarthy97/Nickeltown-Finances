using System.Text.RegularExpressions;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Infrastructure.Import;

public static class SquareDescriptionHelper
{
    private static readonly string[] PitstopItemKeywords =
    [
        "Raffle Ticket",
        "Sausage Sizzle",
        "Stubby Holder",
        "Stein",
        "Beanie",
        "Bar Mat",
        "Banner",
        "Bucket Hat",
        "Car Sticker"
    ];

    private static readonly string[] MemberMerchKeywords =
    [
        "Club Shirt",
        "Flanno",
        "Jacket",
        "Club Sticker"
    ];

    public static (string Description, string CustomerName) Parse(
        string rawDescription,
        string customerNameColumn)
    {
        var text = StripCustomAmountPrefix(rawDescription?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(text))
            text = "Square sale";

        var customerFromColumn = customerNameColumn?.Trim() ?? string.Empty;

        if (text.Contains("Membership", StringComparison.OrdinalIgnoreCase))
            return (text, customerFromColumn);

        if (IsBarLine(text))
            return (text, ExtractBarCustomer(text, customerFromColumn));

        return (text, customerFromColumn);
    }

    public static string GetGroupKey(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return "Other";

        if (description.Contains("Membership", StringComparison.OrdinalIgnoreCase))
            return "Membership";

        if (IsBarLine(description))
            return description.Contains("Bar Tab Payment", StringComparison.OrdinalIgnoreCase)
                ? "Bar Tab Payment"
                : "Bar Top-Up";

        if (IsMemberMerch(description))
            return "Merchandise";

        if (IsPitstop(description))
            return "Pitstop";

        return description;
    }

    public static string ToGroupDisplayName(string groupKey, int count)
    {
        if (string.IsNullOrWhiteSpace(groupKey))
            groupKey = "Other";

        if (groupKey.Equals("Membership", StringComparison.OrdinalIgnoreCase))
            return $"Memberships ({count})";

        if (groupKey.Equals("Bar Top-Up", StringComparison.OrdinalIgnoreCase))
            return $"Bar Top-Ups ({count})";

        if (groupKey.Equals("Bar Tab Payment", StringComparison.OrdinalIgnoreCase))
            return $"Bar Tab Payments ({count})";

        if (groupKey.Equals("Pitstop", StringComparison.OrdinalIgnoreCase))
            return $"Pitstop ({count})";

        if (groupKey.Equals("Merchandise", StringComparison.OrdinalIgnoreCase))
            return $"Member merchandise ({count})";

        var plural = groupKey.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            ? groupKey
            : groupKey + "s";

        return $"{plural} ({count})";
    }

    public static IEnumerable<SquareReportItem> ExpandReportItems(
        string description,
        string customerName,
        decimal grossAmount)
    {
        var text = StripCustomAmountPrefix(description?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(text))
            text = "Square sale";

        if (text.Contains("Membership", StringComparison.OrdinalIgnoreCase))
        {
            yield return new SquareReportItem("Membership", text, 1, grossAmount);
            yield break;
        }

        if (IsBarLine(text))
        {
            var label = text.Contains("Bar Tab Payment", StringComparison.OrdinalIgnoreCase)
                ? "Bar Tab Payment"
                : "Bar Top-Up";
            yield return new SquareReportItem("Bar", label, 1, grossAmount);
            yield break;
        }

        if (IsMemberMerch(text))
        {
            var merchItems = ExpandCatalogItems(text).ToList();
            var merchQty = Math.Max(1, merchItems.Sum(i => i.Quantity));
            foreach (var item in merchItems)
                yield return new SquareReportItem("Merchandise", item.Name, item.Quantity, grossAmount * item.Quantity / merchQty);

            yield break;
        }

        if (IsPitstop(text))
        {
            var items = ExpandPitstopItems(text).ToList();
            if (items.Count == 0)
            {
                yield return new SquareReportItem("Pitstop", text, 1, grossAmount);
                yield break;
            }

            var totalQty = Math.Max(1, items.Sum(i => i.Quantity));
            foreach (var item in items)
                yield return new SquareReportItem("Pitstop", item.Name, item.Quantity, grossAmount * item.Quantity / totalQty);

            yield break;
        }

        yield return new SquareReportItem("Other", text, 1, grossAmount);
    }

    public static IReadOnlyList<SquareBreakdownSection> BuildBreakdownSections(IEnumerable<SquareTransaction> lines)
    {
        var aggregates = new Dictionary<(string Section, string ItemName), (int Quantity, decimal Amount)>();

        foreach (var line in lines)
        {
            foreach (var item in ExpandReportItems(line.Description, line.CustomerName, line.GrossAmount))
            {
                var key = (item.Section, item.ItemName);
                if (!aggregates.TryGetValue(key, out var existing))
                    existing = (0, 0m);

                aggregates[key] = (existing.Quantity + item.Quantity, existing.Amount + item.Amount);
            }
        }

        var sectionOrder = new[] { "Bar", "Membership", "Pitstop", "Merchandise", "Other" };
        var sections = new List<SquareBreakdownSection>();

        foreach (var sectionName in sectionOrder)
        {
            var items = aggregates
                .Where(kvp => kvp.Key.Section.Equals(sectionName, StringComparison.OrdinalIgnoreCase))
                .Select(kvp => new SquareBreakdownLine
                {
                    Section = sectionName,
                    ItemName = kvp.Key.ItemName,
                    Quantity = kvp.Value.Quantity,
                    Amount = kvp.Value.Amount
                })
                .OrderByDescending(i => i.Quantity)
                .ThenBy(i => i.ItemName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (items.Count == 0)
                continue;

            sections.Add(new SquareBreakdownSection
            {
                SectionName = FormatSectionTitle(sectionName),
                Items = items,
                SectionTotal = items.Sum(i => i.Amount)
            });
        }

        return sections;
    }

    public static IReadOnlyList<SquareBreakdownLine> BuildBreakdownLines(IEnumerable<SquareTransaction> lines) =>
        BuildBreakdownSections(lines).SelectMany(section => section.Items).ToList();

    private static string FormatSectionTitle(string section) => section switch
    {
        "Bar" => "Bar top-ups and tab payments",
        "Membership" => "Memberships",
        "Pitstop" => "Pitstop",
        "Merchandise" => "Member merchandise",
        _ => section
    };

    private static bool IsPitstop(string description)
    {
        if (string.IsNullOrWhiteSpace(description) || IsMemberMerch(description))
            return false;

        if (description.Contains("Pitstop", StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var part in SplitParts(description))
        {
            if (ContainsPitstopItemKeyword(part))
                return true;
        }

        return ContainsPitstopItemKeyword(description);
    }

    private static bool IsMemberMerch(string description)
    {
        foreach (var part in SplitParts(description))
        {
            if (ContainsMemberMerchKeyword(part))
                return true;
        }

        return ContainsMemberMerchKeyword(description);
    }

    private static bool ContainsMemberMerchKeyword(string text) =>
        MemberMerchKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsPitstopItemKeyword(string text) =>
        PitstopItemKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<(string Name, int Quantity)> ExpandPitstopItems(string text)
    {
        var payload = text;
        var pitstopIdx = text.IndexOf("Pitstop:", StringComparison.OrdinalIgnoreCase);
        if (pitstopIdx >= 0)
            payload = text[(pitstopIdx + "Pitstop:".Length)..].Trim();

        foreach (var part in SplitParts(payload))
            yield return ParseCatalogPart(part);
    }

    private static IEnumerable<(string Name, int Quantity)> ExpandCatalogItems(string text)
    {
        foreach (var part in SplitParts(text))
            yield return ParseCatalogPart(part);
    }

    private static IEnumerable<string> SplitParts(string text) =>
        text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static (string Name, int Quantity) ParseCatalogPart(string part)
    {
        var qtySuffix = Regex.Match(part, @"^(.+?)\s+x(\d+)$", RegexOptions.IgnoreCase);
        if (qtySuffix.Success &&
            int.TryParse(qtySuffix.Groups[2].Value, out var suffixQty) &&
            suffixQty > 0)
        {
            return (NormaliseCatalogName(qtySuffix.Groups[1].Value.Trim()), suffixQty);
        }

        var qtyPrefix = Regex.Match(part, @"^(\d+)\s+x\s+(.+)$", RegexOptions.IgnoreCase);
        if (qtyPrefix.Success &&
            int.TryParse(qtyPrefix.Groups[1].Value, out var prefixQty) &&
            prefixQty > 0)
        {
            return (NormaliseCatalogName(qtyPrefix.Groups[2].Value.Trim()), prefixQty);
        }

        return (NormaliseCatalogName(part.Trim()), 1);
    }

    private static string NormaliseCatalogName(string name)
    {
        if (name.EndsWith("(Regular)", StringComparison.OrdinalIgnoreCase))
            return name[..^"(Regular)".Length].Trim();

        return name;
    }

    private static bool IsBarLine(string text) =>
        text.Contains("Bar Top-Up", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("Bar Tab Payment", StringComparison.OrdinalIgnoreCase);

    private static string ExtractBarCustomer(string text, string customerNameColumn)
    {
        var dashIdx = text.IndexOf(" - ", StringComparison.Ordinal);
        if (dashIdx > 0 && dashIdx < text.Length - 3)
        {
            var parsed = FormatCustomerName(text[(dashIdx + 3)..].Trim(), string.Empty);
            if (!string.IsNullOrWhiteSpace(parsed))
                return parsed;
        }

        return customerNameColumn;
    }

    private static string StripCustomAmountPrefix(string text)
    {
        const string prefix = "Custom Amount - ";
        return text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? text[prefix.Length..].Trim()
            : text;
    }

    private static string FormatCustomerName(string fromDetails, string customerNameColumn)
    {
        if (!string.IsNullOrWhiteSpace(fromDetails))
        {
            var commaIdx = fromDetails.IndexOf(',');
            if (commaIdx >= 0 && commaIdx < fromDetails.Length - 1)
                return fromDetails[(commaIdx + 1)..].Trim();

            return fromDetails.Trim();
        }

        return customerNameColumn;
    }
}

public readonly record struct SquareReportItem(string Section, string ItemName, int Quantity, decimal Amount);