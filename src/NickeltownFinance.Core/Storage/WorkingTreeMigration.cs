namespace NickeltownFinance.Core.Storage;

/// <summary>
/// Copies receipts, exports, logs, and backups out of Documents into LocalAppData.
/// Documents is often OneDrive, which denies access while it syncs and was stopping
/// startup (backups) and club upload (receipt previews). The Documents copies stay
/// so OneDrive does not delete them from the other computer. New reads and writes
/// use the local copies.
/// </summary>
public static class WorkingTreeMigration
{
    public static readonly string[] FolderNames = ["files", "exports", "logs", "backups"];

    public sealed class Result
    {
        public int CopiedFiles { get; set; }

        public List<string> SkippedPaths { get; } = [];
    }

    public static Result EnsureLocalCopy(
        string localRoot,
        string? legacyRoot,
        Func<string, IEnumerable<string>>? listEntries = null)
    {
        Directory.CreateDirectory(localRoot);
        var result = new Result();
        if (string.IsNullOrWhiteSpace(legacyRoot)
            || BackupFolderPolicy.PathsEqual(localRoot, legacyRoot)
            || !Directory.Exists(legacyRoot))
            return result;

        var list = listEntries ?? Directory.EnumerateFileSystemEntries;
        foreach (var name in FolderNames)
        {
            var source = Path.Combine(legacyRoot, name);
            if (!Directory.Exists(source))
                continue;

            CopyDirectory(source, Path.Combine(localRoot, name), result, list);
        }

        return result;
    }

    private static void CopyDirectory(
        string source,
        string destination,
        Result result,
        Func<string, IEnumerable<string>> listEntries)
    {
        try
        {
            Directory.CreateDirectory(destination);
        }
        catch (Exception ex) when (StorageAccess.IsAccessProblem(ex))
        {
            result.SkippedPaths.Add(destination);
            return;
        }

        IReadOnlyList<string> entries;
        try
        {
            entries = listEntries(source).ToList();
        }
        catch (Exception ex) when (StorageAccess.IsAccessProblem(ex))
        {
            result.SkippedPaths.Add(source);
            return;
        }

        foreach (var entry in entries)
        {
            if (entry.EndsWith(".migrating", StringComparison.OrdinalIgnoreCase))
                continue;

            bool isDirectory;
            try
            {
                isDirectory = Directory.Exists(entry);
            }
            catch (Exception ex) when (StorageAccess.IsAccessProblem(ex))
            {
                result.SkippedPaths.Add(entry);
                continue;
            }

            var name = Path.GetFileName(entry);
            if (string.IsNullOrEmpty(name))
                continue;

            if (isDirectory)
                CopyDirectory(entry, Path.Combine(destination, name), result, listEntries);
            else
                CopyFile(entry, Path.Combine(destination, name), result);
        }
    }

    private static void CopyFile(string source, string destination, Result result)
    {
        try
        {
            if (File.Exists(destination))
                return;
        }
        catch (Exception ex) when (StorageAccess.IsAccessProblem(ex))
        {
            result.SkippedPaths.Add(source);
            return;
        }

        var temp = destination + ".migrating";
        try
        {
            var directory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                input.CopyTo(output);
            }

            try
            {
                File.Move(temp, destination, overwrite: false);
            }
            catch (IOException) when (File.Exists(destination))
            {
                TryDelete(temp);
                return;
            }

            result.CopiedFiles++;
        }
        catch (Exception ex) when (StorageAccess.IsAccessProblem(ex) || ex is IOException)
        {
            TryDelete(temp);
            result.SkippedPaths.Add(source);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // A leftover staging file must not block the next launch.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
