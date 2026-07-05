namespace NickeltownFinance.Core.Update;

public enum AppInstallKind
{
    Portable,
    Msix
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
