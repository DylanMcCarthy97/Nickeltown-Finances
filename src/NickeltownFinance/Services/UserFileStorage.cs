using LiteDB;
using NickeltownFinance.Core.Constants;

namespace NickeltownFinance.Services;

/// <summary>Copies user images (profile photo, signature) into app data for stable report paths.</summary>
public static class UserFileStorage
{
    public static string StoreSignature(ObjectId userId, string sourcePath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Signature image not found.", sourcePath);

        var dir = Path.Combine(AppPaths.FilesRoot, "signatures");
        Directory.CreateDirectory(dir);

        var ext = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".png";
        ext = ext.ToLowerInvariant();

        // Always write a new file name so we never overwrite a file still mapped by the UI.
        var dest = Path.Combine(dir, $"{userId}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}");
        CopyUnlocked(sourcePath, dest);
        CleanupOldFiles(dir, userId.ToString(), dest);
        return dest;
    }

    public static string StoreProfilePicture(ObjectId userId, string sourcePath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Profile picture not found.", sourcePath);

        var dir = Path.Combine(AppPaths.FilesRoot, "avatars");
        Directory.CreateDirectory(dir);

        var ext = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".png";
        ext = ext.ToLowerInvariant();

        var dest = Path.Combine(dir, $"{userId}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}");
        CopyUnlocked(sourcePath, dest);
        CleanupOldFiles(dir, userId.ToString(), dest);
        return dest;
    }

    private static void CopyUnlocked(string sourcePath, string destPath)
    {
        // Read all bytes then write — avoids sharing issues with temp/source files.
        var bytes = File.ReadAllBytes(sourcePath);
        File.WriteAllBytes(destPath, bytes);
    }

    private static void CleanupOldFiles(string directory, string userIdPrefix, string keepPath)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                var name = Path.GetFileName(file);
                if (!name.StartsWith(userIdPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(file, keepPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // File may still be displayed; leave it for a later cleanup.
                }
            }
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }
}
