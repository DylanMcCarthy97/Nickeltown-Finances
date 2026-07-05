using System.Text;

namespace NickeltownFinance.Core.Security;

public static partial class UsernameGenerator
{
    /// <summary>
    /// Builds a login username from a full name, e.g. "Jane Smith" → "jane.smith".
    /// </summary>
    public static string FromFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return string.Empty;

        var parts = fullName
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CleanPart)
            .Where(p => p.Length > 0)
            .ToArray();

        if (parts.Length == 0)
            return string.Empty;

        return string.Join('.', parts).ToLowerInvariant();
    }

    /// <summary>
    /// Returns <paramref name="preferred"/>, or preferred2 / preferred3 / … until <paramref name="isAvailable"/> is true.
    /// </summary>
    public static string EnsureUnique(string preferred, Func<string, bool> isAvailable)
    {
        if (string.IsNullOrWhiteSpace(preferred))
            preferred = "user";

        preferred = preferred.Trim().ToLowerInvariant();
        if (isAvailable(preferred))
            return preferred;

        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{preferred}{i}";
            if (isAvailable(candidate))
                return candidate;
        }

        return $"{preferred}{Guid.NewGuid():N}"[..16];
    }

    private static string CleanPart(string part)
    {
        var sb = new StringBuilder(part.Length);
        foreach (var ch in part.Normalize(NormalizationForm.FormD))
        {
            if (char.IsAsciiLetterOrDigit(ch))
                sb.Append(ch);
        }

        return sb.ToString();
    }
}
