using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NickeltownFinance.Core.Constants;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Interfaces;

namespace NickeltownFinance.Infrastructure.Services;

public class ClubCloudSyncService : IClubCloudSyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex ClubIdPattern = new(
        "^[a-z0-9]([a-z0-9-]{1,62}[a-z0-9])$",
        RegexOptions.Compiled);

    private readonly IBackupService _backupService;
    private readonly HttpClient _httpClient;
    private readonly object _stateLock = new();

    public ClubCloudSyncService(IBackupService backupService)
    {
        _backupService = backupService;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    }

    public ClubCloudSyncStatus GetStatus()
    {
        var prefs = LoadPreferences();
        return new ClubCloudSyncStatus
        {
            Enabled = prefs.Enabled,
            ServerUrl = prefs.ServerUrl,
            ClubId = prefs.ClubId,
            HasSyncCode = !string.IsNullOrWhiteSpace(prefs.SyncCode),
            LocalRevision = Math.Max(prefs.LastPulledRevision, prefs.LastPushedRevision),
            StatusSummary = BuildLocalSummary(prefs)
        };
    }

    public async Task<ClubCloudSyncStatus> RefreshRemoteStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = GetStatus();
        var prefs = LoadPreferences();
        if (!status.IsConfigured)
            return status;

        try
        {
            using var request = CreateRequest(HttpMethod.Get, prefs, $"/v1/clubs/{prefs.ClubId}/status");
            using var response = await SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                status.StatusSummary = "Club not on the server yet. Use Sync now on this PC to publish the first copy.";
                return status;
            }

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<StatusApiResponse>(stream, JsonOptions, cancellationToken);
            var remote = payload?.Manifest;
            if (remote is null || payload?.HasPackage != true)
            {
                status.StatusSummary = "Server is reachable. No package published yet — use Sync now to upload.";
                return status;
            }

            status.Remote = new ClubCloudRemoteManifest
            {
                Revision = remote.Revision,
                WrittenUtc = remote.WrittenUtc,
                DeviceId = remote.DeviceId,
                MachineName = remote.MachineName,
                PackageBytes = remote.PackageBytes
            };

            var fromOther = !string.Equals(remote.DeviceId, prefs.DeviceId, StringComparison.OrdinalIgnoreCase);
            status.NewerRemoteAvailable = fromOther && remote.Revision > prefs.LastPulledRevision;
            status.StatusSummary = status.NewerRemoteAvailable
                ? $"Newer club data is online from '{remote.MachineName}' (revision {remote.Revision}). Pull to use it here."
                : $"Online copy is up to date (revision {remote.Revision}).";
            return status;
        }
        catch (Exception ex)
        {
            status.LastError = ex.Message;
            status.StatusSummary = $"Could not reach the sync server: {ex.Message}";
            return status;
        }
    }

    public void SavePreferences(bool enabled, string serverUrl, string clubId, string syncCode)
    {
        lock (_stateLock)
        {
            var prefs = LoadPreferencesUnlocked();
            prefs.Enabled = enabled;
            prefs.ServerUrl = NormalizeServerUrl(serverUrl);
            prefs.ClubId = NormalizeClubId(clubId);
            if (!string.IsNullOrWhiteSpace(syncCode))
                prefs.SyncCode = syncCode.Trim();
            EnsureDeviceId(prefs);
            SavePreferencesUnlocked(prefs);
        }
    }

    public Task<ClubCloudSyncResult> PushAsync(bool force = false, CancellationToken cancellationToken = default) =>
        PushCoreAsync(force, cancellationToken, existingBackupPath: null);

    private async Task<ClubCloudSyncResult> PushCoreAsync(
        bool force,
        CancellationToken cancellationToken,
        string? existingBackupPath)
    {
        var prefs = LoadPreferences();
        if (!prefs.Enabled)
            return new ClubCloudSyncResult { Skipped = true, Message = "Internet sync is turned off." };

        var validation = ValidateConfigured(prefs);
        if (validation is not null)
            return new ClubCloudSyncResult { Success = false, Message = validation };

        var nextRevision = Math.Max(prefs.LastPulledRevision, prefs.LastPushedRevision) + 1;
        // If we know remote is ahead and we're not forcing, try status first for clearer errors.
        if (!force)
        {
            var remoteStatus = await RefreshRemoteStatusAsync(cancellationToken);
            if (remoteStatus.Remote is { } remote
                && remote.Revision > prefs.LastPulledRevision
                && !string.Equals(remote.DeviceId, prefs.DeviceId, StringComparison.OrdinalIgnoreCase))
            {
                return new ClubCloudSyncResult
                {
                    Success = false,
                    Conflict = true,
                    Revision = remote.Revision,
                    Message =
                        $"The online copy from '{remote.MachineName}' is newer (revision {remote.Revision}). " +
                        "Pull first, or force overwrite."
                };
            }

            if (remoteStatus.Remote is not null)
                nextRevision = Math.Max(nextRevision, remoteStatus.Remote.Revision + 1);
        }

        var backupPath = !string.IsNullOrWhiteSpace(existingBackupPath) && File.Exists(existingBackupPath)
            ? existingBackupPath
            : await _backupService.CreateBackupAsync(isManual: false);
        var encrypted = EncryptFile(backupPath, prefs.SyncCode);
        try
        {
            using var request = CreateRequest(HttpMethod.Put, prefs, $"/v1/clubs/{prefs.ClubId}/package");
            request.Headers.TryAddWithoutValidation("X-Base-Revision", prefs.LastPulledRevision.ToString());
            request.Headers.TryAddWithoutValidation("X-Revision", nextRevision.ToString());
            request.Headers.TryAddWithoutValidation("X-Device-Id", prefs.DeviceId);
            request.Headers.TryAddWithoutValidation("X-Machine-Name", Environment.MachineName);
            if (force)
                request.Headers.TryAddWithoutValidation("X-Force", "true");

            await using var file = File.OpenRead(encrypted);
            request.Content = new StreamContent(file);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using var response = await SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                return new ClubCloudSyncResult
                {
                    Success = false,
                    Conflict = true,
                    Message = "The online copy changed on another computer. Pull first, or force overwrite."
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return new ClubCloudSyncResult
                {
                    Success = false,
                    Message = $"Upload failed ({(int)response.StatusCode}): {TrimError(body)}"
                };
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var manifest = await JsonSerializer.DeserializeAsync<ManifestDto>(stream, JsonOptions, cancellationToken);
            var revision = manifest?.Revision ?? nextRevision;

            lock (_stateLock)
            {
                var latest = LoadPreferencesUnlocked();
                latest.LastPulledRevision = revision;
                latest.LastPushedRevision = revision;
                SavePreferencesUnlocked(latest);
            }

            return new ClubCloudSyncResult
            {
                Success = true,
                Revision = revision,
                Message = $"Published revision {revision} to the sync server."
            };
        }
        finally
        {
            TryDelete(encrypted);
        }
    }

    public async Task<ClubCloudSyncResult> PullAsync(CancellationToken cancellationToken = default)
    {
        var prefs = LoadPreferences();
        var validation = ValidateConfigured(prefs);
        if (validation is not null)
            return new ClubCloudSyncResult { Success = false, Message = validation };

        using var request = CreateRequest(HttpMethod.Get, prefs, $"/v1/clubs/{prefs.ClubId}/package");
        using var response = await SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return new ClubCloudSyncResult { Success = false, Message = "No online club package found yet." };

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return new ClubCloudSyncResult
            {
                Success = false,
                Message = $"Download failed ({(int)response.StatusCode}): {TrimError(body)}"
            };
        }

        var tempEncrypted = Path.Combine(Path.GetTempPath(), $"nickeltown_pull_{Guid.NewGuid():N}.bin");
        var tempZip = Path.Combine(Path.GetTempPath(), $"nickeltown_pull_{Guid.NewGuid():N}.zip");
        try
        {
            await using (var output = File.Create(tempEncrypted))
            {
                await response.Content.CopyToAsync(output, cancellationToken);
            }

            DecryptFile(tempEncrypted, tempZip, prefs.SyncCode);
            await _backupService.RestoreBackupAsync(tempZip);

            // Read status for revision bookmark.
            var status = await RefreshRemoteStatusAsync(cancellationToken);
            var revision = status.Remote?.Revision
                           ?? Math.Max(prefs.LastPulledRevision, prefs.LastPushedRevision) + 1;

            lock (_stateLock)
            {
                var latest = LoadPreferencesUnlocked();
                latest.LastPulledRevision = revision;
                latest.LastPushedRevision = revision;
                // Keep prefs — restore replaces LiteDB only.
                latest.Enabled = prefs.Enabled;
                latest.ServerUrl = prefs.ServerUrl;
                latest.ClubId = prefs.ClubId;
                latest.SyncCode = prefs.SyncCode;
                EnsureDeviceId(latest);
                SavePreferencesUnlocked(latest);
            }

            return new ClubCloudSyncResult
            {
                Success = true,
                RestartRequired = true,
                Revision = revision,
                Message = $"Loaded revision {revision} from the sync server. The app will restart."
            };
        }
        finally
        {
            TryDelete(tempEncrypted);
            TryDelete(tempZip);
        }
    }

    public async Task PushOnShutdownAsync(string? backupZipPath = null)
    {
        try
        {
            // Conflicts are left for the next launch prompt. The zip was taken while the
            // database was still open; uploading it does not need the books file.
            _ = await PushCoreAsync(force: false, CancellationToken.None, backupZipPath);
        }
        catch
        {
            // Shutdown must not throw.
        }
    }

    public ClubCloudSyncResult? GetPendingPullOffer()
    {
        // Synchronous offer uses cached prefs only; UI/startup should call RefreshRemoteStatusAsync.
        var prefs = LoadPreferences();
        if (!prefs.Enabled || ValidateConfigured(prefs) is not null)
            return null;
        return null;
    }

    public static string SuggestClubId(string? clubName)
    {
        if (string.IsNullOrWhiteSpace(clubName))
            return "my-club";

        var slug = Regex.Replace(clubName.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-");
        slug = slug.Trim('-');
        if (slug.Length < 3)
            slug = "club-" + slug;
        if (slug.Length > 64)
            slug = slug[..64].Trim('-');
        return ClubIdPattern.IsMatch(slug) ? slug : "my-club";
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

    private static HttpRequestMessage CreateRequest(HttpMethod method, ClubCloudSyncPreferences prefs, string path)
    {
        var url = prefs.ServerUrl.TrimEnd('/') + path;
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("X-Club-Sync-Code", prefs.SyncCode);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static string? ValidateConfigured(ClubCloudSyncPreferences prefs)
    {
        if (string.IsNullOrWhiteSpace(prefs.ServerUrl))
            return "Enter the sync server URL.";
        if (string.IsNullOrWhiteSpace(prefs.ClubId) || !ClubIdPattern.IsMatch(prefs.ClubId))
            return "Club id must be lowercase letters, numbers, and hyphens (at least 3 characters).";
        if (string.IsNullOrWhiteSpace(prefs.SyncCode) || prefs.SyncCode.Length < 8)
            return "Choose a sync code of at least 8 characters. Use the same code on both computers.";
        return null;
    }

    private static string BuildLocalSummary(ClubCloudSyncPreferences prefs)
    {
        if (!prefs.Enabled)
            return "Internet sync is off. Turn it on so home and club stay on the same logins and books.";
        if (ValidateConfigured(prefs) is { } error)
            return error;
        return $"Ready to sync club '{prefs.ClubId}' via {prefs.ServerUrl}.";
    }

    private ClubCloudSyncPreferences LoadPreferences()
    {
        lock (_stateLock)
            return LoadPreferencesUnlocked();
    }

    private ClubCloudSyncPreferences LoadPreferencesUnlocked()
    {
        try
        {
            if (File.Exists(AppPaths.ClubCloudSyncStatePath))
            {
                var json = File.ReadAllText(AppPaths.ClubCloudSyncStatePath);
                var prefs = JsonSerializer.Deserialize<ClubCloudSyncPreferences>(json, JsonOptions);
                if (prefs is not null)
                {
                    EnsureDeviceId(prefs);
                    return prefs;
                }
            }
        }
        catch
        {
            // Fall through.
        }

        var fresh = new ClubCloudSyncPreferences();
        EnsureDeviceId(fresh);
        return fresh;
    }

    private void SavePreferencesUnlocked(ClubCloudSyncPreferences prefs)
    {
        EnsureDeviceId(prefs);
        var dir = Path.GetDirectoryName(AppPaths.ClubCloudSyncStatePath)!;
        Directory.CreateDirectory(dir);
        var temp = AppPaths.ClubCloudSyncStatePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(prefs, JsonOptions));
        File.Copy(temp, AppPaths.ClubCloudSyncStatePath, overwrite: true);
        File.Delete(temp);
    }

    private static void EnsureDeviceId(ClubCloudSyncPreferences prefs)
    {
        if (string.IsNullOrWhiteSpace(prefs.DeviceId))
            prefs.DeviceId = Guid.NewGuid().ToString("N");
    }

    private static string NormalizeServerUrl(string serverUrl)
    {
        var url = (serverUrl ?? string.Empty).Trim().TrimEnd('/');
        return url;
    }

    private static string NormalizeClubId(string clubId) =>
        (clubId ?? string.Empty).Trim().ToLowerInvariant();

    private static string EncryptFile(string inputPath, string syncCode)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"nickeltown_enc_{Guid.NewGuid():N}.bin");
        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var key = DeriveKey(syncCode, salt);

        var plain = File.ReadAllBytes(inputPath);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(key, 16))
            aes.Encrypt(nonce, plain, cipher, tag);

        using var output = File.Create(outputPath);
        output.Write(Encoding.ASCII.GetBytes("NCFS1")); // magic + version
        output.Write(salt);
        output.Write(nonce);
        output.Write(tag);
        output.Write(cipher);
        CryptographicOperations.ZeroMemory(key);
        return outputPath;
    }

    private static void DecryptFile(string inputPath, string outputZipPath, string syncCode)
    {
        var bytes = File.ReadAllBytes(inputPath);
        if (bytes.Length < 5 + 16 + 12 + 16)
            throw new InvalidOperationException("Online package is corrupt or incomplete.");

        var magic = Encoding.ASCII.GetString(bytes, 0, 5);
        if (magic != "NCFS1")
            throw new InvalidOperationException("Online package is not a Nickeltown sync file.");

        var salt = bytes.AsSpan(5, 16).ToArray();
        var nonce = bytes.AsSpan(21, 12).ToArray();
        var tag = bytes.AsSpan(33, 16).ToArray();
        var cipher = bytes.AsSpan(49).ToArray();
        var key = DeriveKey(syncCode, salt);
        var plain = new byte[cipher.Length];

        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(nonce, cipher, tag, plain);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("Could not decrypt the online package. Check the sync code.", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        File.WriteAllBytes(outputZipPath, plain);
    }

    private static byte[] DeriveKey(string syncCode, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(syncCode),
            salt,
            iterations: 120_000,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 32);
    }

    private static string TrimError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "Unknown error.";
        body = body.Trim();
        return body.Length <= 240 ? body : body[..240] + "…";
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }

    private sealed class StatusApiResponse
    {
        public bool Exists { get; set; }
        public bool HasPackage { get; set; }
        public ManifestDto? Manifest { get; set; }
    }

    private sealed class ManifestDto
    {
        public long Revision { get; set; }
        public DateTime WrittenUtc { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public long PackageBytes { get; set; }
    }
}
