namespace NickeltownFinance.Core.Storage;

/// <summary>
/// Turns raw filesystem failures into a message a treasurer can act on.
/// OneDrive denies access to Documents while it syncs, and that used to surface
/// as "Access to the path '\\?\C:\Users\...\' is denied."
/// </summary>
public static class StorageAccess
{
    public static bool IsAccessProblem(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is UnauthorizedAccessException)
                return true;

            if (current is not IOException io)
                continue;

            var code = io.HResult & 0xFFFF;
            if (code is 5 or 32 or 33)
                return true;

            if (io.Message.Contains("is denied", StringComparison.OrdinalIgnoreCase)
                || io.Message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase)
                || io.Message.Contains("used by another process", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static bool IsAccessDenied(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is UnauthorizedAccessException)
                return true;

            if (current.Message.Contains("is denied", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("Access to the path", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Full sentence for the user. Access errors name the folder and what to do.
    /// Anything else is the original message, with <paramref name="intro"/> in front when set.
    /// </summary>
    public static string ForUser(Exception exception, string? intro = null)
    {
        if (TryExplainAccessDenied(exception, out var explained))
            return explained;

        var message = string.IsNullOrWhiteSpace(exception.Message)
            ? "Something went wrong."
            : exception.Message.Trim();

        return string.IsNullOrWhiteSpace(intro) ? message : intro.Trim() + "\n\n" + message;
    }

    public static string NormalizePath(string path)
    {
        var value = path.Trim().Trim('\'', '"');
        if (value.StartsWith(@"\\?\", StringComparison.Ordinal))
            value = value[4..];
        return value;
    }

    private static bool TryExplainAccessDenied(Exception exception, out string message)
    {
        message = string.Empty;
        if (!IsAccessDenied(exception))
            return false;

        var path = ExtractPath(exception.Message);
        var display = path is null ? null : NormalizePath(path);
        var cloud = display is not null && DatabaseInUseException.IsCloudSyncedPath(display);

        if (cloud)
        {
            message =
                "OneDrive blocked Nickeltown Finance from a folder in Documents.\n\n" +
                display +
                "\n\nBooks, receipts, and backups stay on this PC, outside OneDrive. " +
                "Pause OneDrive for a minute and open the app again. " +
                "The club does not sync through OneDrive.";
            return true;
        }

        message = display is null
            ? "Nickeltown Finance could not use a folder on this PC. Check that Windows is allowing the app to write there, then try again."
            : "Nickeltown Finance could not use this folder:\n\n" +
              display +
              "\n\nCheck that Windows is allowing the app to write there, then try again.";
        return true;
    }

    private static string? ExtractPath(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        var marker = "Access to the path ";
        var start = message.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;

        start += marker.Length;
        if (start >= message.Length)
            return null;

        var quote = message[start];
        if (quote is '\'' or '"')
        {
            var end = message.IndexOf(quote, start + 1);
            if (end > start)
                return message[(start + 1)..end];
        }

        return null;
    }
}
