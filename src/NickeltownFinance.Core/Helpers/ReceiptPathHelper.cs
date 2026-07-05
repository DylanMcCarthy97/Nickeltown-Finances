using NickeltownFinance.Core.Constants;

namespace NickeltownFinance.Core.Helpers;

/// <summary>
/// Safe path resolution and file writes for receipt processing (Unicode/long Windows paths).
/// </summary>
public static class ReceiptPathHelper
{
    /// <summary>Leave headroom below MAX_PATH for temp copies and suffixes.</summary>
    public const int SafePathLength = 240;

    public static string GetItemDirectory(LiteDB.ObjectId itemId) =>
        Path.Combine(AppPaths.InboxPath, itemId.ToString());

    public static string GetOriginalPath(LiteDB.ObjectId itemId, string extension) =>
        Path.Combine(GetItemDirectory(itemId), $"original{extension}");

    /// <summary>Colour-enhanced preview for user viewing (never OCR preprocessing).</summary>
    public static string GetPreviewPath(LiteDB.ObjectId itemId) =>
        Path.Combine(GetItemDirectory(itemId), "preview.jpg");

    /// <summary>Internal OCR-optimised image — never shown to users by default.</summary>
    public static string GetOcrImagePath(LiteDB.ObjectId itemId) =>
        Path.Combine(GetItemDirectory(itemId), "ocr.jpg");

    /// <summary>Legacy alias — maps to preview.jpg.</summary>
    public static string GetProcessedPath(LiteDB.ObjectId itemId) => GetPreviewPath(itemId);

    public static string GetThumbnailPath(LiteDB.ObjectId itemId) =>
        Path.Combine(GetItemDirectory(itemId), "thumbnail.jpg");

    public static bool TryResolveFilePath(string? relativePath, out string fullPath, out string? error)
    {
        fullPath = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            error = "Relative path is empty.";
            return false;
        }

        fullPath = AppPaths.ResolvePath(relativePath);
        if (Directory.Exists(fullPath))
        {
            error = $"Path is a directory, not a file: {fullPath}";
            fullPath = string.Empty;
            return false;
        }

        if (!File.Exists(fullPath))
        {
            error = $"File not found: {fullPath}";
            return false;
        }

        return true;
    }

    public static void EnsureDirectoryForFile(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
    }

    public static void WriteBytes(string outputFilePath, byte[] bytes)
    {
        EnsureDirectoryForFile(outputFilePath);
        File.WriteAllBytes(outputFilePath, bytes);
    }

    public static bool TryCopyFile(string sourcePath, string destinationPath, out string? error)
    {
        error = null;
        try
        {
            if (!File.Exists(sourcePath))
            {
                error = $"Source file not found: {sourcePath}";
                return false;
            }

            EnsureDirectoryForFile(destinationPath);
            File.Copy(sourcePath, destinationPath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// WinRT OCR APIs fail on missing, empty, or very long paths — copy to a short temp file when needed.
    /// </summary>
    public static async Task<(string Path, bool IsTemp)> PrepareOcrPathAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new FileNotFoundException("OCR source file not found.", sourcePath);

        if (sourcePath.Length <= SafePathLength)
            return (sourcePath, false);

        var tempDir = Path.Combine(Path.GetTempPath(), "NickeltownFinance", "ocr");
        Directory.CreateDirectory(tempDir);
        var ext = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".jpg";

        var tempPath = Path.Combine(tempDir, $"{Guid.NewGuid():N}{ext}");
        await using (var source = File.OpenRead(sourcePath))
        await using (var dest = File.Create(tempPath))
            await source.CopyToAsync(dest, cancellationToken);

        return (tempPath, true);
    }

    public static void TryDeleteTempFile(string? path, bool isTemp)
    {
        if (!isTemp || string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best effort
        }
    }
}
