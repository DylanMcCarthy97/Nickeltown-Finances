using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using NickeltownFinance.Core;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Update;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using CoreAppInfo = NickeltownFinance.Core.AppInfo;

namespace NickeltownFinance.Services;

[SupportedOSPlatform("windows10.0.17763.0")]
public sealed class AppUpdateService : IAppUpdateService
{
    private static readonly HttpClient DownloadClient = CreateDownloadClient();

    private readonly IGitHubReleaseClient _releaseClient;
    private readonly ILogger<AppUpdateService> _logger;

    public AppUpdateService(IGitHubReleaseClient releaseClient, ILogger<AppUpdateService> logger)
    {
        _releaseClient = releaseClient;
        _logger = logger;
    }

    public string CurrentVersion => CoreAppInfo.Version;

    public AppInstallKind InstallKind => IsPackaged() ? AppInstallKind.Msix : AppInstallKind.Portable;

    public async Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        if (!UpdateConstants.IsConfigured)
        {
            return new AppUpdateCheckResult(
                false,
                false,
                null,
                "Update feed is not configured. Set GitHubOwner and GitHubRepo in UpdateConstants.cs.");
        }

        try
        {
            if (IsPackaged())
            {
                var storeResult = await CheckStoreUpdateAsync(cancellationToken);
                if (storeResult is not null)
                    return storeResult;
            }

            var latest = await _releaseClient.GetLatestReleaseAsync(cancellationToken);
            if (latest is null)
            {
                return new AppUpdateCheckResult(
                    false,
                    false,
                    null,
                    "Could not reach GitHub releases. Check your internet connection and repository settings.");
            }

            var updateAvailable = AppUpdateVersion.IsNewer(CurrentVersion, latest.Version);
            return new AppUpdateCheckResult(true, updateAvailable, updateAvailable ? latest : null, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Update check failed");
            return new AppUpdateCheckResult(false, false, null, ex.Message);
        }
    }

    public async Task<AppUpdateApplyResult> DownloadAndApplyUpdateAsync(
        AppUpdateInfo update,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(update.MsixDownloadUrl))
        {
            return new AppUpdateApplyResult(
                false,
                false,
                "This release does not include an MSIX package. Download it manually from the release page.");
        }

        try
        {
            progress?.Report("Downloading update…");
            var packagePath = await DownloadPackageAsync(update, cancellationToken);

            progress?.Report("Installing update…");
            if (IsPackaged())
            {
                await ApplyMsixUpdateAsync(packagePath, cancellationToken);
                return new AppUpdateApplyResult(true, true, null);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = packagePath,
                UseShellExecute = true
            });

            return new AppUpdateApplyResult(true, true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to apply update {Version}", update.Version);
            return new AppUpdateApplyResult(false, false, ex.Message);
        }
    }

    public Task OpenReleaseNotesAsync(AppUpdateInfo update, CancellationToken cancellationToken = default)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = update.ReleasePageUrl,
            UseShellExecute = true
        });
        return Task.CompletedTask;
    }

    private async Task<AppUpdateCheckResult?> CheckStoreUpdateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var package = Package.Current;
            var result = await package.CheckUpdateAvailabilityAsync();
            if (result.Availability != PackageUpdateAvailability.Available)
                return null;

            var latest = await _releaseClient.GetLatestReleaseAsync(cancellationToken);
            if (latest is null)
            {
                return new AppUpdateCheckResult(
                    true,
                    true,
                    new AppUpdateInfo(
                        "Unknown",
                        "update",
                        "An update is available through the installed package feed.",
                        null,
                        UpdateConstants.IsConfigured
                            ? $"https://github.com/{UpdateConstants.GitHubOwner}/{UpdateConstants.GitHubRepo}/releases/latest"
                            : string.Empty,
                        DateTimeOffset.UtcNow),
                    null);
            }

            return new AppUpdateCheckResult(true, true, latest, null);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Package update availability check failed; falling back to GitHub.");
            return null;
        }
    }

    private static async Task<string> DownloadPackageAsync(AppUpdateInfo update, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(Path.GetTempPath(), "NickeltownFinance", "updates");
        Directory.CreateDirectory(folder);

        var extension = update.MsixDownloadUrl!.Contains(".msixbundle", StringComparison.OrdinalIgnoreCase)
            ? ".msixbundle"
            : ".msix";
        var fileName = $"NickeltownFinance-{update.Version}{extension}";
        var destination = Path.Combine(folder, fileName);

        if (File.Exists(destination))
            File.Delete(destination);

        using var response = await DownloadClient.GetAsync(update.MsixDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(destination);
        await input.CopyToAsync(output, cancellationToken);
        return destination;
    }

    private static async Task ApplyMsixUpdateAsync(string packagePath, CancellationToken cancellationToken)
    {
        var uri = new Uri(Path.GetFullPath(packagePath));
        var result = await new PackageManager().AddPackageAsync(
            uri,
            null,
            DeploymentOptions.ForceApplicationShutdown).AsTask().WaitAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(result.ErrorText))
            throw new InvalidOperationException(result.ErrorText);
    }

    private static bool IsPackaged()
    {
        try
        {
            _ = Package.Current;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static HttpClient CreateDownloadClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UpdateConstants.UserAgent, "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        return client;
    }
}
