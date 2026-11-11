namespace NickeltownFinance.Core.Storage;

/// <summary>
/// Copies an existing books file out of Documents (often OneDrive) into LocalAppData
/// before LiteDB opens it. The Documents copy is left in place so OneDrive does not
/// delete it from another computer.
/// </summary>
public static class DatabaseFileMigration
{
    public static void EnsureLocalCopy(
        string databasePath,
        string? legacyDatabasePath,
        TimeSpan retryFor,
        Action<int>? delay = null,
        Func<string, bool>? validate = null)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (string.IsNullOrEmpty(directory))
            throw new InvalidOperationException("Database path has no folder.");

        Directory.CreateDirectory(directory);

        var staging = Path.Combine(
            directory,
            Path.GetFileNameWithoutExtension(databasePath) + ".migrating" + Path.GetExtension(databasePath));
        var stagingLog = GetLogFilePath(staging);

        if (File.Exists(databasePath))
        {
            TryDelete(staging);
            TryDelete(stagingLog);
            TryDelete(GetTempFilePath(staging));
            return;
        }

        if (string.IsNullOrWhiteSpace(legacyDatabasePath) || !File.Exists(legacyDatabasePath))
            return;

        if (PathsEqual(databasePath, legacyDatabasePath))
            return;

        TryDelete(staging);
        TryDelete(stagingLog);
        TryDelete(GetTempFilePath(staging));

        var committed = false;
        try
        {
            ExecuteWithRetry(retryFor, () => CopyShared(legacyDatabasePath, staging), delay);

            // Validate the data file on its own. A log beside the staging name would
            // make LiteDB replay it, and a locked or half-written log must not fail the copy.
            TryDelete(stagingLog);
            TryDelete(GetTempFilePath(staging));

            if (validate is not null && !validate(staging))
            {
                throw new InvalidOperationException(
                    "Nickeltown Finance found your existing books but could not read them:\n" +
                    legacyDatabasePath +
                    "\n\nThe original file was left unchanged.");
            }

            TryDelete(stagingLog);
            TryDelete(GetTempFilePath(staging));

            try
            {
                File.Move(staging, databasePath, overwrite: false);
            }
            catch (IOException) when (File.Exists(databasePath))
            {
                TryDelete(staging);
                TryDelete(stagingLog);
                return;
            }

            committed = true;

            // Do not copy nickeltown-log.db. That log is only valid together with the
            // data file from the same instant. The data file is the last checkpoint.
        }
        catch (Exception ex) when (!committed)
        {
            TryDelete(staging);
            TryDelete(stagingLog);
            TryDelete(GetTempFilePath(staging));
            if (IsSharingViolation(ex))
                throw new DatabaseInUseException(legacyDatabasePath, ex);
            throw;
        }
    }

    /// <summary>LiteDB log name: nickeltown.db → nickeltown-log.db.</summary>
    public static string GetLogFilePath(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(databasePath) + "-log" + Path.GetExtension(databasePath);
        return Path.Combine(directory, name);
    }

    public static string GetTempFilePath(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(databasePath) + "-tmp" + Path.GetExtension(databasePath);
        return Path.Combine(directory, name);
    }

    public static bool IsSharingViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is not IOException io)
                continue;

            var code = io.HResult & 0xFFFF;
            if (code is 32 or 33)
                return true;

            if (io.Message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase)
                || io.Message.Contains("used by another process", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static void ExecuteWithRetry(TimeSpan retryFor, Action action, Action<int>? delay = null)
    {
        delay ??= attempt => Thread.Sleep(TimeSpan.FromMilliseconds(Math.Min(500, 150 * (attempt + 1))));
        var started = Environment.TickCount64;
        var attempt = 0;

        while (true)
        {
            try
            {
                action();
                return;
            }
            catch (IOException ex) when (IsSharingViolation(ex))
            {
                var elapsed = Environment.TickCount64 - started;
                if (elapsed >= retryFor.TotalMilliseconds)
                    throw;

                delay(attempt++);
            }
        }
    }

    private static void CopyShared(string source, string destination)
    {
        var directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        input.CopyTo(output);
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
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
    }
}
