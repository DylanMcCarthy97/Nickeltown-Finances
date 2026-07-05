using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NickeltownFinance.Infrastructure.Import;

public static class ImportFingerprint
{
    public static string Compute(DateTime date, decimal amount, string description, string reference)
    {
        var payload = string.Join('|',
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            amount.ToString("0.00", CultureInfo.InvariantCulture),
            Normalize(description),
            Normalize(reference));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }

    public static string Normalize(string value) =>
        string.Join(' ', (value ?? string.Empty)
            .Trim()
            .ToUpperInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
