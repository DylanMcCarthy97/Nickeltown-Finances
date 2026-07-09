namespace NickeltownFinance.Infrastructure.Import;

public static class SquareDescriptionHelper
{
    public static (string Description, string CustomerName) Parse(
        string tenderNote,
        string description,
        string customerNameColumn)
    {
        var source = !string.IsNullOrWhiteSpace(tenderNote) ? tenderNote.Trim()
            : !string.IsNullOrWhiteSpace(description) ? description.Trim()
            : "Square sale";

        source = StripCustomAmountPrefix(source);

        var dashIdx = source.IndexOf(" - ", StringComparison.Ordinal);
        if (dashIdx > 0)
        {
            var desc = source[..dashIdx].Trim();
            var customerPart = source[(dashIdx + 3)..].Trim();
            return (desc, FormatCustomerName(customerPart, customerNameColumn));
        }

        return (source, FormatCustomerName(string.Empty, customerNameColumn));
    }

    public static string ToGroupDisplayName(string description, int count)
    {
        if (string.IsNullOrWhiteSpace(description))
            description = "Other";

        var plural = description.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            ? description
            : description + "s";

        return $"{plural} ({count})";
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

        return customerNameColumn?.Trim() ?? string.Empty;
    }
}