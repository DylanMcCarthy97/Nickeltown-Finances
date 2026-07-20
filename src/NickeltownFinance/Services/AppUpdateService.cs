using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Versioning;
using System.Text;
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
        IProgress<AppUpdateProgress>? progress = null,
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
            progress?.Report(new AppUpdateProgress(AppUpdateStage.Downloading, 0, "Downloading update…"));
            var packagePath = await DownloadPackageAsync(update, progress, cancellationToken);

            // Schedule relaunch before install — ForceApplicationShutdown often kills this process mid-call.
            progress?.Report(new AppUpdateProgress(AppUpdateStage.Installing, 0, "Preparing restart…"));
            ScheduleRelaunchAfterExit();

            progress?.Report(new AppUpdateProgress(AppUpdateStage.Installing, 0, "Installing update…"));
            if (IsPackaged())
            {
                await ApplyMsixUpdateAsync(packagePath, progress, cancellationToken);
                progress?.Report(new AppUpdateProgress(AppUpdateStage.Restarting, 100, "Update installed. Restarting…"));
                return new AppUpdateApplyResult(true, true, null);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = packagePath,
                UseShellExecute = true
            });

            progress?.Report(new AppUpdateProgress(
                AppUpdateStage.Restarting,
                100,
                "App Installer opened. This app will close and reopen after install."));
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

    private static async Task<string> DownloadPackageAsync(
        AppUpdateInfo update,
        IProgress<AppUpdateProgress>? progress,
        CancellationToken cancellationToken)
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

        using var response = await DownloadClient.GetAsync(
            update.MsixDownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(destination);

        var buffer = new byte[81920];
        long bytesRead = 0;
        var lastReportedPercent = -1;
        var lastReportUtc = DateTime.MinValue;

        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            bytesRead += read;

            var now = DateTime.UtcNow;
            if (totalBytes is > 0)
            {
                var percent = Math.Clamp(100.0 * bytesRead / totalBytes.Value, 0, 100);
                var wholePercent = (int)percent;
                if (wholePercent != lastReportedPercent || (now - lastReportUtc).TotalMilliseconds >= 250)
                {
                    lastReportedPercent = wholePercent;
                    lastReportUtc = now;
                    progress?.Report(new AppUpdateProgress(
                        AppUpdateStage.Downloading,
                        percent,
                        $"Downloading update… {wholePercent}% ({FormatBytes(bytesRead)} / {FormatBytes(totalBytes.Value)})"));
                }
            }
            else if ((now - lastReportUtc).TotalMilliseconds >= 400)
            {
                lastReportUtc = now;
                progress?.Report(new AppUpdateProgress(
                    AppUpdateStage.Downloading,
                    null,
                    $"Downloading update… {FormatBytes(bytesRead)}"));
            }
        }

        progress?.Report(new AppUpdateProgress(
            AppUpdateStage.Downloading,
            100,
            $"Download complete ({FormatBytes(bytesRead)})."));
        return destination;
    }

    private static async Task ApplyMsixUpdateAsync(
        string packagePath,
        IProgress<AppUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(Path.GetFullPath(packagePath));
        var operation = new PackageManager().AddPackageAsync(
            uri,
            null,
            DeploymentOptions.ForceApplicationShutdown | DeploymentOptions.ForceUpdateFromAnyVersion);

        operation.Progress = (_, deploymentProgress) =>
        {
            var percent = Math.Clamp((double)deploymentProgress.percentage, 0, 100);
            progress?.Report(new AppUpdateProgress(
                AppUpdateStage.Installing,
                percent,
                $"Installing update… {(int)percent}%"));
        };

        var result = await operation.AsTask().WaitAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(result.ErrorText))
            throw new InvalidOperationException(result.ErrorText);
    }

    /// <summary>
    /// Starts a detached helper that waits for this process to exit, then relaunches the app
    /// via execution alias and Appx package fallbacks. Must run before ForceApplicationShutdown.
    /// </summary>
    private void ScheduleRelaunchAfterExit()
    {
        try
        {
            var folder = Path.Combine(Path.GetTempPath(), "NickeltownFinance", "updates");
            Directory.CreateDirectory(folder);

            var scriptPath = Path.Combine(folder, "relaunch-after-update.cmd");
            var pid = Environment.ProcessId;
            string? packageFamilyName = null;
            try
            {
                if (IsPackaged())
                    packageFamilyName = Package.Current.Id.FamilyName;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not resolve package family name for relaunch.");
            }

            var portableExe = Environment.ProcessPath;

            var script = new StringBuilder();
            script.AppendLine("@echo off");
            script.AppendLine("setlocal EnableExtensions");
            script.AppendLine($":wait_exit");
            script.AppendLine($"tasklist /FI \"PID eq {pid}\" 2>NUL | find \"{pid}\" >NUL");
            script.AppendLine("if not errorlevel 1 (");
            script.AppendLine("  timeout /t 1 /nobreak >NUL");
            script.AppendLine("  goto wait_exit");
            script.AppendLine(")");
            script.AppendLine("rem Give deployment a moment to finish registering the package.");
            script.AppendLine("timeout /t 3 /nobreak >NUL");
            script.AppendLine();
            script.AppendLine("set /a attempts=0");
            script.AppendLine(":try_launch");
            script.AppendLine("set /a attempts+=1");
            script.AppendLine("if %attempts% GTR 40 goto give_up");
            script.AppendLine();
            script.AppendLine("where nickeltownfinance.exe >NUL 2>&1");
            script.AppendLine("if not errorlevel 1 (");
            script.AppendLine("  start \"\" nickeltownfinance.exe");
            script.AppendLine("  exit /b 0");
            script.AppendLine(")");
            script.AppendLine();

            if (!string.IsNullOrWhiteSpace(packageFamilyName))
            {
                script.AppendLine($"start \"\" explorer.exe shell:AppsFolder\\{packageFamilyName}!App");
                script.AppendLine("timeout /t 2 /nobreak >NUL");
                script.AppendLine("tasklist /FI \"IMAGENAME eq NickeltownFinance.exe\" 2>NUL | find /I \"NickeltownFinance.exe\" >NUL");
                script.AppendLine("if not errorlevel 1 exit /b 0");
            }

            script.AppendLine();
            script.AppendLine("for /f \"usebackq delims=\" %%P in (`powershell -NoProfile -Command \"(Get-AppxPackage -Name NickeltownFinance | Select-Object -First 1).PackageFamilyName\"`) do (");
            script.AppendLine("  if not \"%%P\"==\"\" (");
            script.AppendLine("    start \"\" explorer.exe shell:AppsFolder\\%%P!App");
            script.AppendLine("    timeout /t 2 /nobreak >NUL");
            script.AppendLine("    tasklist /FI \"IMAGENAME eq NickeltownFinance.exe\" 2>NUL | find /I \"NickeltownFinance.exe\" >NUL");
            script.AppendLine("    if not errorlevel 1 exit /b 0");
            script.AppendLine("  )");
            script.AppendLine(")");
            script.AppendLine();

            if (!string.IsNullOrWhiteSpace(portableExe) && File.Exists(portableExe))
            {
                script.AppendLine($"if exist \"{portableExe}\" (");
                script.AppendLine($"  start \"\" \"{portableExe}\"");
                script.AppendLine("  exit /b 0");
                script.AppendLine(")");
                script.AppendLine();
            }

            script.AppendLine("timeout /t 3 /nobreak >NUL");
            script.AppendLine("goto try_launch");
            script.AppendLine();
            script.AppendLine(":give_up");
            script.AppendLine("exit /b 1");

            File.WriteAllText(scriptPath, script.ToString(), Encoding.ASCII);

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{scriptPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            _logger.LogInformation(
                "Scheduled post-update relaunch helper for PID {Pid} (family={FamilyName})",
                pid,
                packageFamilyName ?? "(unknown)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to schedule post-update relaunch helper.");
        }
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

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes} {units[unit]}"
            : string.Create(CultureInfo.InvariantCulture, $"{value:0.#} {units[unit]}");
    }

    private static HttpClient CreateDownloadClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UpdateConstants.UserAgent, "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        return client;
    }
}
