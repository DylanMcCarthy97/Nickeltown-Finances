namespace NickeltownFinance.Core.Storage;

/// <summary>
/// Lists files under a folder without aborting when one subdirectory is locked.
/// <see cref="Directory.GetFiles(string, string, SearchOption)"/> throws
/// <see cref="UnauthorizedAccessException"/> for the first denied child and drops the rest,
/// which failed club sync when OneDrive blocked a single previews folder.
/// </summary>
public static class FileTreeWalker
{
    public static IReadOnlyList<string> ListFiles(
        string root,
        ICollection<string>? skipped = null,
        Func<string, IEnumerable<string>>? listEntries = null)
    {
        var files = new List<string>();
        if (string.IsNullOrWhiteSpace(root))
            return files;

        listEntries ??= Directory.EnumerateFileSystemEntries;
        Walk(root, files, skipped, listEntries);
        return files;
    }

    private static void Walk(
        string directory,
        List<string> files,
        ICollection<string>? skipped,
        Func<string, IEnumerable<string>> listEntries)
    {
        IReadOnlyList<string> entries;
        try
        {
            entries = listEntries(directory).ToList();
        }
        catch (Exception ex) when (StorageAccess.IsAccessProblem(ex))
        {
            skipped?.Add(directory);
            return;
        }

        foreach (var entry in entries)
        {
            bool isDirectory;
            try
            {
                isDirectory = Directory.Exists(entry);
            }
            catch (Exception ex) when (StorageAccess.IsAccessProblem(ex))
            {
                skipped?.Add(entry);
                continue;
            }

            if (isDirectory)
                Walk(entry, files, skipped, listEntries);
            else
                files.Add(entry);
        }
    }
}
