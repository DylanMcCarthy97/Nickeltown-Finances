namespace NickeltownFinance.Core.Storage;

/// <summary>
/// The books file could not be opened because another process already has it.
/// </summary>
public sealed class DatabaseInUseException : IOException
{
    public DatabaseInUseException(string path, Exception inner)
        : base(BuildMessage(path), inner)
    {
        DatabasePath = path;
        HResult = inner.HResult;
    }

    public string DatabasePath { get; }

    public static string BuildMessage(string path)
    {
        var cloud = IsCloudSyncedPath(path)
            ? "\n\nThat file is inside a cloud folder (OneDrive). Pause syncing for a minute so it lets go of the file, then open Nickeltown Finance again."
            : string.Empty;

        return
            "The books file is open in another program, so Nickeltown Finance cannot start.\n\n" +
            path +
            "\n\nClose any other Nickeltown Finance window. If you do not see one, open Task Manager, end every Nickeltown Finance process, and try again." +
            cloud;
    }

    public static bool IsCloudSyncedPath(string path) =>
        path.Contains("OneDrive", StringComparison.OrdinalIgnoreCase)
        || path.Contains("Dropbox", StringComparison.OrdinalIgnoreCase)
        || path.Contains("Google Drive", StringComparison.OrdinalIgnoreCase)
        || path.Contains("iCloud", StringComparison.OrdinalIgnoreCase);
}
