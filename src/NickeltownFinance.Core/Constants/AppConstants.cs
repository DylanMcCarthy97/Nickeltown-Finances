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
    public static string AppDataRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NickeltownFinance");

    public static string DatabasePath => Path.Combine(AppDataRoot, "data", "nickeltown.db");

    public static string FilesRoot => Path.Combine(AppDataRoot, "files");

    public static string AttachmentsPath => Path.Combine(FilesRoot, "attachments");

    public static string InboxPath => Path.Combine(FilesRoot, "inbox");

    public static string ThumbnailsPath => Path.Combine(FilesRoot, "thumbnails");

    public static string ExportsPath => Path.Combine(AppDataRoot, "exports");

    public static string LogsPath => Path.Combine(AppDataRoot, "logs");

    public static string ResolvePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Relative path cannot be empty.", nameof(relativePath));

        return Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.Combine(AppDataRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
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
        var root = AppDataRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                   + Path.DirectorySeparatorChar;
        if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return fullPath[root.Length..].Replace('\\', '/');
        return fullPath;
    }
}
