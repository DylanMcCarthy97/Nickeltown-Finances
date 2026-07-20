namespace NickeltownFinance.Core.Update;

public enum AppInstallKind
{
    Portable,
    Msix
}

public enum AppUpdateStage
{
    Downloading,
    Installing,
    Restarting
}

public sealed record AppUpdateInfo(
    string Version,
    string ReleaseTag,
    string ReleaseNotes,
    string? MsixDownloadUrl,
    string ReleasePageUrl,
    DateTimeOffset PublishedAt);

public sealed record AppUpdateCheckResult(
    bool CheckSucceeded,
    bool UpdateAvailable,
    AppUpdateInfo? Update,
    string? ErrorMessage);

public sealed record AppUpdateApplyResult(
    bool Success,
    bool RestartRequired,
    string? ErrorMessage);

/// <param name="PercentComplete">0–100 when known; null for indeterminate stages.</param>
public sealed record AppUpdateProgress(
    AppUpdateStage Stage,
    double? PercentComplete,
    string Message);
