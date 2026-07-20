using NickeltownFinance.Core.Update;

namespace NickeltownFinance.Core.Interfaces;

public interface IAppUpdateService
{
    string CurrentVersion { get; }

    AppInstallKind InstallKind { get; }

    Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    Task<AppUpdateApplyResult> DownloadAndApplyUpdateAsync(
        AppUpdateInfo update,
        IProgress<AppUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task OpenReleaseNotesAsync(AppUpdateInfo update, CancellationToken cancellationToken = default);
}

public interface IGitHubReleaseClient
{
    Task<AppUpdateInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken = default);
}
