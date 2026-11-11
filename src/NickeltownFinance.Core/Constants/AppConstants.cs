namespace NickeltownFinance.Core.Constants;

public static class SettingKeys
{
    public const string ClubName = "ClubName";
    public const string ClubLogoPath = "ClubLogoPath";
    public const string TreasurerSignaturePath = "TreasurerSignaturePath";
    public const string FinancialYearStartMonth = "FinancialYearStartMonth";
    public const string TrackingStartDate = "TrackingStartDate";
    public const string TrackingStartBalance = "TrackingStartBalance";
    public const string Theme = "Theme";
    public const string BackupFolder = "BackupFolder";
    public const string LastUsername = "LastUsername";
    public const string RememberUsername = "RememberUsername";
    public const string IsInitialized = "IsInitialized";
    public const string AuthVersion = "AuthVersion";
    public const string WindowLeft = "WindowLeft";
    public const string WindowTop = "WindowTop";
    public const string WindowWidth = "WindowWidth";
    public const string WindowHeight = "WindowHeight";
    public const string WindowState = "WindowState";
    public const string SidebarCollapsed = "SidebarCollapsed";
    public const string MobileUploadPort = "MobileUploadPort";
    public const string MobileUploadEnabled = "MobileUploadEnabled";
    public const string ReceiptAutoEnhancement = "ReceiptAutoEnhancement";
    public const string ReceiptOcrEnabled = "ReceiptOcrEnabled";
    public const string ReceiptAiCategorisation = "ReceiptAiCategorisation";
    public const string ReceiptBankMatching = "ReceiptBankMatching";
    public const string ReceiptDuplicateDetection = "ReceiptDuplicateDetection";
    public const string ReceiptThumbnailGeneration = "ReceiptThumbnailGeneration";
    public const string DefaultCashOnHand = "DefaultCashOnHand";
    public const string DefaultShireBonds = "DefaultShireBonds";
    public const string DefaultPayPalBalance = "DefaultPayPalBalance";
}


public static class AppPaths
{
    /// <summary>
    /// Previous home under Documents. Windows often redirects Documents into OneDrive,
    /// which locks files and denies access. Kept only so an existing copy can be moved
    /// out on launch. New writes do not go here.
    /// </summary>
    public static string LegacyAppDataRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NickeltownFinance");

    /// <summary>
    /// Books, receipts, backups, and logs. LocalApplicationData is outside OneDrive,
    /// so club sync and startup are not blocked when Documents is syncing.
    /// </summary>
    public static string LocalDataRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NickeltownFinance");

    public static string AppDataRoot => LocalDataRoot;

    public static string DatabasePath => Path.Combine(LocalDataRoot, "data", "nickeltown.db");

    /// <summary>
    /// Previous books file, under Documents. Copied into <see cref="DatabasePath"/> once.
    /// </summary>
    public static string LegacyDatabasePath =>
        Path.Combine(LegacyAppDataRoot, "data", "nickeltown.db");

    public static string FilesRoot => Path.Combine(AppDataRoot, "files");

    public static string AttachmentsPath => Path.Combine(FilesRoot, "attachments");

    public static string MonthDocumentsPath => Path.Combine(FilesRoot, "month-documents");

    public static string InboxPath => Path.Combine(FilesRoot, "inbox");

    public static string ThumbnailsPath => Path.Combine(FilesRoot, "thumbnails");

    public static string ExportsPath => Path.Combine(AppDataRoot, "exports");

    public static string LogsPath => Path.Combine(AppDataRoot, "logs");

    /// <summary>
    /// Machine-local internet sync preferences (server URL, club id, sync code, revisions).
    /// Kept outside LiteDB so a second PC can join before it has club data.
    /// </summary>
    public static string ClubCloudSyncStatePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NickeltownFinance",
            "club-cloud-sync.json");

    public static string ResolvePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Relative path cannot be empty.", nameof(relativePath));

        if (Path.IsPathRooted(relativePath))
            return PreferLocalCopy(relativePath);

        var relative = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var local = Path.Combine(AppDataRoot, relative);
        if (PathExists(local))
            return local;

        var legacy = Path.Combine(LegacyAppDataRoot, relative);
        if (PathExists(legacy))
            return legacy;

        return local;
    }

    /// <summary>
    /// An absolute path saved under Documents is served from LocalAppData once that
    /// copy exists, so a receipt is not opened from OneDrive after the move.
    /// </summary>
    private static string PreferLocalCopy(string fullPath)
    {
        if (!TryStripRoot(LegacyAppDataRoot, fullPath, out var relative))
            return fullPath;

        var local = Path.Combine(AppDataRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        if (PathExists(local))
            return local;
        if (PathExists(fullPath))
            return fullPath;
        return local;
    }

    private static bool PathExists(string path)
    {
        try
        {
            return File.Exists(path) || Directory.Exists(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool TryResolvePath(string? relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath))
            return false;

        try
        {
            fullPath = ResolvePath(relativePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string ToRelativePath(string fullPath)
    {
        if (TryStripRoot(AppDataRoot, fullPath, out var relative))
            return relative;
        if (TryStripRoot(LegacyAppDataRoot, fullPath, out relative))
            return relative;
        return fullPath;
    }

    private static bool TryStripRoot(string root, string fullPath, out string relative)
    {
        relative = string.Empty;
        try
        {
            var rootFull = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(fullPath);
            if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                return false;

            relative = full[rootFull.Length..].Replace('\\', '/');
            return true;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }
}
