namespace NickeltownFinance.Core.Update;

public static class AppUpdateVersion
{
    public static string? Normalize(string? versionOrTag)
    {
        if (string.IsNullOrWhiteSpace(versionOrTag))
            return null;

        var trimmed = versionOrTag.Trim();
        if (trimmed.StartsWith('v') || trimmed.StartsWith('V'))
            trimmed = trimmed[1..];

        // Strip SemVer build metadata (e.g. 1.0.0+gitsha) before parsing.
        var plusIndex = trimmed.IndexOf('+');
        if (plusIndex >= 0)
            trimmed = trimmed[..plusIndex];

        return Version.TryParse(trimmed, out var version)
            ? version.ToString(version.Revision >= 0 ? 4 : 3)
            : null;
    }

    public static bool IsNewer(string currentVersion, string latestVersion)
    {
        if (!Version.TryParse(Normalize(currentVersion) ?? currentVersion, out var current))
            return false;

        if (!Version.TryParse(Normalize(latestVersion) ?? latestVersion, out var latest))
            return false;

        return latest > current;
    }
}
