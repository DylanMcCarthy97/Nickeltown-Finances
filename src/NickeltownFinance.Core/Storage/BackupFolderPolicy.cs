namespace NickeltownFinance.Core.Storage;

/// <summary>
/// Backup zips used to live in Documents, which Windows often redirects into OneDrive.
/// OneDrive denying that folder stopped the app from starting. Backups now stay beside
/// the books file unless the treasurer picked a folder outside a cloud drive.
/// </summary>
public static class BackupFolderPolicy
{
    public static bool IsCloudPath(string? path) =>
        !string.IsNullOrWhiteSpace(path) && DatabaseInUseException.IsCloudSyncedPath(path);

    public static bool IsUnder(string? path, string? root)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
            return false;

        try
        {
            var full = WithSeparator(Path.GetFullPath(path));
            var rootFull = WithSeparator(Path.GetFullPath(root));
            return full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    public static bool ShouldRetarget(string? configured, string legacyRoot, string localDefault)
    {
        if (string.IsNullOrWhiteSpace(configured))
            return true;

        if (PathsEqual(configured, localDefault))
            return false;

        return IsUnder(configured, legacyRoot) || IsCloudPath(configured);
    }

    /// <summary>
    /// Returns a folder that can be created. Cloud and Documents locations are replaced
    /// with <paramref name="localDefault"/> before anything is created there.
    /// </summary>
    public static string ResolveWritableFolder(string? configured, string legacyRoot, string localDefault)
    {
        var folder = ShouldRetarget(configured, legacyRoot, localDefault)
            ? localDefault
            : configured!.Trim();

        Directory.CreateDirectory(folder);
        return folder;
    }

    public static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        try
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
        {
            return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string WithSeparator(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed + Path.DirectorySeparatorChar;
    }
}
