using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LiteDB;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NickeltownFinance.Core.Constants;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;

namespace NickeltownFinance.Infrastructure.Services.ReceiptImport;

public sealed class MobileUploadHost : IMobileUploadHost, IAsyncDisposable
{
    private const int DefaultPort = 7842;
    private const int TokenLifetimeMinutes = 5;
    private const int MaxUploadBytes = 25 * 1024 * 1024;
    private const int MaxUploadsPerSession = 20;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".webp", ".heic", ".tiff", ".tif"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/heic",
        "image/tiff",
        "application/octet-stream"
    };

    private readonly ISettingsRepository _settings;
    private readonly IReceiptImportService _importService;
    private readonly IReceiptImportQueue _queue;
    private readonly ILogger<MobileUploadHost> _logger;
    private readonly object _lock = new();

    private WebApplication? _app;
    private UploadSession? _session;
    private int _listeningPort;

    public MobileUploadHost(
        ISettingsRepository settings,
        IReceiptImportService importService,
        IReceiptImportQueue queue,
        ILogger<MobileUploadHost> logger)
    {
        _settings = settings;
        _importService = importService;
        _queue = queue;
        _logger = logger;
    }

    public bool IsRunning
    {
        get
        {
            lock (_lock)
                return _session is not null && _session.ExpiresAt > DateTime.UtcNow;
        }
    }

    public bool IsServerListening
    {
        get
        {
            lock (_lock)
                return _app is not null;
        }
    }

    public MobileUploadSessionInfo? CurrentSession
    {
        get
        {
            lock (_lock)
            {
                if (_session is null || _session.ExpiresAt <= DateTime.UtcNow)
                    return null;
                return ToInfo(_session);
            }
        }
    }

    public async Task<MobileUploadSessionInfo> StartSessionAsync(
        MobileUploadSessionRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureServerRunningAsync(cancellationToken);

        lock (_lock)
        {
            _session = CreateSession(_listeningPort, request);
            _logger.LogInformation(
                "Mobile upload session started on {Ip}:{Port}, expires {Expires:u}, sessionKey={SessionKey}, txn={TransactionId}",
                _session.LocalIp,
                _session.Port,
                _session.ExpiresAt,
                _session.SessionKey ?? "(none)",
                _session.TargetTransactionId?.ToString() ?? "(none)");
            return ToInfo(_session);
        }
    }

    public async Task<MobileUploadSessionInfo> RefreshSessionAsync(
        MobileUploadSessionRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureServerRunningAsync(cancellationToken);

        lock (_lock)
        {
            if (_app is null)
                throw new InvalidOperationException("Mobile upload server is not running.");

            var port = _listeningPort;
            var ip = GetLocalIPv4() ?? "127.0.0.1";
            var preservedKey = _session?.SessionKey;
            var preservedTxn = _session?.TargetTransactionId;
            var preservedTarget = _session?.ImportTarget ?? ReceiptImportTarget.Inbox;
            _session = new UploadSession
            {
                Token = GenerateToken(),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(TokenLifetimeMinutes),
                AllowedNetworkPrefix = GetNetworkPrefix(ip),
                Port = port,
                LocalIp = ip,
                UploadCount = 0,
                SessionKey = request?.SessionKey ?? preservedKey,
                TargetTransactionId = request?.TargetTransactionId ?? preservedTxn,
                ImportTarget = request?.ImportTarget ?? preservedTarget
            };

            _logger.LogInformation("Mobile upload session token refreshed, expires {Expires:u}", _session.ExpiresAt);
            return ToInfo(_session);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
            _session = null;

        await StopServerAsync(cancellationToken);
    }

    public bool TryValidateUpload(string token, string clientIp, out string? failureReason)
    {
        lock (_lock)
        {
            if (_session is null)
            {
                failureReason = "Upload session is not active.";
                return false;
            }

            if (_session.ExpiresAt <= DateTime.UtcNow)
            {
                failureReason = "Upload token has expired.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(token) ||
                !FixedTimeEquals(_session.Token, token))
            {
                failureReason = "Invalid upload token.";
                return false;
            }

            if (!IsAllowedClientIp(clientIp, _session.AllowedNetworkPrefix))
            {
                failureReason = "Uploads are only allowed from the local network.";
                return false;
            }

            if (_session.UploadCount >= MaxUploadsPerSession)
            {
                failureReason = "Upload limit reached for this session.";
                return false;
            }

            failureReason = null;
            return true;
        }
    }

    public string MapClientError(string? failureReason) => failureReason switch
    {
        null or "" => "Upload failed, try again.",
        var r when r.Contains("expired", StringComparison.OrdinalIgnoreCase) =>
            "Upload session expired.",
        var r when r.Contains("local network", StringComparison.OrdinalIgnoreCase) =>
            "Not on same Wi-Fi/network.",
        var r when r.Contains("not active", StringComparison.OrdinalIgnoreCase) =>
            "Desktop app not reachable.",
        var r when r.Contains("Invalid", StringComparison.OrdinalIgnoreCase) =>
            "Invalid upload session.",
        var r when r.Contains("too large", StringComparison.OrdinalIgnoreCase) =>
            "File too large.",
        var r when r.Contains("Unsupported", StringComparison.OrdinalIgnoreCase) =>
            "Unsupported file type.",
        var r when r.Contains("limit", StringComparison.OrdinalIgnoreCase) =>
            "Upload limit reached. Refresh the QR code on the desktop app.",
        _ => failureReason
    };

    private static int MapValidationStatusCode(string? failureReason)
    {
        if (failureReason is not null &&
            failureReason.Contains("local network", StringComparison.OrdinalIgnoreCase))
            return StatusCodes.Status403Forbidden;

        if (failureReason is not null &&
            failureReason.Contains("limit", StringComparison.OrdinalIgnoreCase))
            return StatusCodes.Status429TooManyRequests;

        return StatusCodes.Status401Unauthorized;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        if (_app is not null)
            await _app.DisposeAsync();
    }

    private async Task EnsureServerRunningAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_app is not null)
                return;
        }

        var portSetting = _settings.GetValue<int>(SettingKeys.MobileUploadPort);
        var port = portSetting == 0 ? DefaultPort : portSetting;

        try
        {
            var wwwroot = ResolveWwwRootPath();
            if (!Directory.Exists(wwwroot))
                throw new DirectoryNotFoundException($"Mobile upload wwwroot not found: {wwwroot}");

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = Array.Empty<string>(),
                ContentRootPath = AppContext.BaseDirectory,
                ApplicationName = typeof(MobileUploadHost).Assembly.FullName
            });

            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(IPAddress.Any, port);
            });
            builder.WebHost.UseSetting(WebHostDefaults.SuppressStatusMessagesKey, "True");

            var app = builder.Build();
            ConfigureMiddleware(app, wwwroot);
            ConfigureRoutes(app);

            await app.StartAsync(cancellationToken);

            lock (_lock)
            {
                _app = app;
                _listeningPort = port;
            }

            _logger.LogInformation("Mobile upload server listening on http://0.0.0.0:{Port}", port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start mobile upload server on port {Port}", port);
            throw new InvalidOperationException(
                $"Could not start mobile upload server on port {port}. {ex.Message}", ex);
        }
    }

    private async Task StopServerAsync(CancellationToken cancellationToken)
    {
        WebApplication? app;
        lock (_lock)
        {
            app = _app;
            _app = null;
        }

        if (app is null) return;

        try
        {
            await app.StopAsync(cancellationToken);
            await app.DisposeAsync();
            _logger.LogInformation("Mobile upload server stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping mobile upload server");
        }
    }

    private void ConfigureMiddleware(WebApplication app, string wwwroot)
    {
        app.Use(async (context, next) =>
        {
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Append("X-Frame-Options", "DENY");
            context.Response.Headers.Append("Cache-Control", "no-store");
            await next();
        });

        app.UseDefaultFiles(new DefaultFilesOptions
        {
            FileProvider = new PhysicalFileProvider(wwwroot),
            RequestPath = string.Empty
        });

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(wwwroot),
            RequestPath = string.Empty
        });
    }

    private void ConfigureRoutes(WebApplication app)
    {
        app.MapGet("/upload", async context =>
        {
            var path = Path.Combine(ResolveWwwRootPath(), "upload.html");
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(path);
        });

        app.MapGet("/api/session", (HttpContext context) =>
        {
            var token = ExtractToken(context);
            var clientIp = GetClientIp(context);

            if (!TryValidateUpload(token, clientIp, out var reason))
            {
                return Results.Json(new
                {
                    valid = false,
                    error = MapClientError(reason)
                }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var session = CurrentSession!;
            return Results.Json(new
            {
                valid = true,
                expiresAt = session.ExpiresAt,
                uploadUrl = session.UploadUrl,
                importTarget = session.ImportTarget.ToString()
            });
        });

        app.MapPost("/api/upload", async (HttpContext context) => await UploadAsync(context));

        app.MapPost("/api/retry/{importItemId}", async (HttpContext context, string importItemId) =>
        {
            var token = ExtractToken(context);
            var clientIp = GetClientIp(context);

            if (!TryValidateUpload(token, clientIp, out var reason))
            {
                return Results.Json(new
                {
                    success = false,
                    error = MapClientError(reason)
                }, statusCode: StatusCodes.Status401Unauthorized);
            }

            ObjectId id;
            try
            {
                id = new ObjectId(importItemId);
            }
            catch
            {
                return Results.Json(new { success = false, error = "Invalid import item id." }, statusCode: StatusCodes.Status400BadRequest);
            }

            await _importService.RetryAsync(id);
            var status = await GetUploadStatusAsync(id);
            return Results.Json(new { success = true, status });
        });

        app.MapGet("/api/status/{importItemId}", async (HttpContext context, string importItemId) =>
        {
            var token = ExtractToken(context);
            var clientIp = GetClientIp(context);

            if (!TryValidateUpload(token, clientIp, out var reason))
            {
                return Results.Json(new
                {
                    valid = false,
                    error = MapClientError(reason)
                }, statusCode: StatusCodes.Status401Unauthorized);
            }

            ObjectId id;
            try
            {
                id = new ObjectId(importItemId);
            }
            catch
            {
                return Results.Json(new { error = "Invalid import item id." }, statusCode: StatusCodes.Status400BadRequest);
            }

            var status = await GetUploadStatusAsync(id);
            return status is null
                ? Results.Json(new { error = "Import item not found." }, statusCode: StatusCodes.Status404NotFound)
                : Results.Json(status);
        });
    }

    public async Task<ReceiptUploadStatusInfo?> GetUploadStatusAsync(ObjectId importItemId)
    {
        var item = await _importService.GetByIdAsync(importItemId);
        if (item is null) return null;

        var isTransaction = item.ImportTarget == ReceiptImportTarget.Transaction;
        var processingComplete = isTransaction
            ? item.Status is ReceiptImportStatus.Committed or ReceiptImportStatus.Failed
            : item.Status is ReceiptImportStatus.Ready
                or ReceiptImportStatus.CompletedWithWarnings
                or ReceiptImportStatus.Failed
                or ReceiptImportStatus.Committed;

        return new ReceiptUploadStatusInfo
        {
            ImportItemId = item.Id.ToString(),
            UploadSucceeded = true,
            Status = item.Status.ToString(),
            StatusDisplay = MapPhoneStatusDisplay(item),
            Stage = item.StatusDisplay,
            ErrorMessage = item.ErrorMessage,
            ProcessingComplete = processingComplete,
            ProcessingFailed = item.Status == ReceiptImportStatus.Failed,
            CanRetry = item.Status == ReceiptImportStatus.Failed,
            Supplier = item.DetectedSupplierName ?? item.Ocr?.EffectiveSupplier,
            Amount = item.Ocr?.Total,
            Currency = item.Ocr?.Currency,
            ImportTarget = item.ImportTarget.ToString(),
            IsAttached = item.Status == ReceiptImportStatus.Committed
        };
    }

    private static string MapPhoneStatusDisplay(ReceiptImportItemInfo item) => item.Status switch
    {
        ReceiptImportStatus.Queued => "Uploaded — waiting to process",
        ReceiptImportStatus.Uploading => "Receiving upload…",
        ReceiptImportStatus.Preprocessing => "Image enhancement…",
        ReceiptImportStatus.ProcessingOcr => "OCR…",
        ReceiptImportStatus.SupplierDetection => "Supplier detection…",
        ReceiptImportStatus.AiParsing => "Analysing…",
        ReceiptImportStatus.MatchingTransaction => "Bank matching…",
        ReceiptImportStatus.GeneratingThumbnail => "Generating thumbnail…",
        ReceiptImportStatus.Ready when item.ImportTarget == ReceiptImportTarget.Transaction =>
            "Processed — save the expense on desktop to attach",
        ReceiptImportStatus.Ready => "Completed",
        ReceiptImportStatus.CompletedWithWarnings when item.ImportTarget == ReceiptImportTarget.Transaction =>
            "Processed — save the expense on desktop to attach",
        ReceiptImportStatus.CompletedWithWarnings => "Completed with warnings",
        ReceiptImportStatus.Failed => "Receipt uploaded successfully. Processing failed.",
        ReceiptImportStatus.Committed when item.ImportTarget == ReceiptImportTarget.Transaction =>
            "Receipt attached successfully",
        ReceiptImportStatus.Committed => "Attached to transaction",
        _ => item.StatusDisplay
    };

    private async Task<IResult> UploadAsync(HttpContext context)
    {
        var token = ExtractToken(context);
        var clientIp = GetClientIp(context);

        if (!TryValidateUpload(token, clientIp, out var reason))
        {
            var statusCode = MapValidationStatusCode(reason);
            _logger.LogWarning(
                "Mobile upload rejected: {Reason} (HTTP {Status}) from {ClientIp}",
                reason,
                statusCode,
                clientIp);
            return Results.Json(new
            {
                success = false,
                error = MapClientError(reason)
            }, statusCode: statusCode);
        }

        _logger.LogInformation("Mobile upload received from {ClientIp}", clientIp);

        if (!context.Request.HasFormContentType)
        {
            return Results.Json(new
            {
                success = false,
                error = "Invalid upload request."
            }, statusCode: StatusCodes.Status400BadRequest);
        }

        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
        {
            return Results.Json(new
            {
                success = false,
                error = "No file was uploaded."
            }, statusCode: StatusCodes.Status400BadRequest);
        }

        if (file.Length > MaxUploadBytes)
        {
            return Results.Json(new
            {
                success = false,
                error = MapClientError("File too large.")
            }, statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        var safeName = SanitizeFileName(file.FileName);
        var ext = Path.GetExtension(safeName);
        if (!AllowedExtensions.Contains(ext))
        {
            return Results.Json(new
            {
                success = false,
                error = MapClientError("Unsupported file type.")
            }, statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? GuessContentType(ext)
            : file.ContentType;

        if (!AllowedContentTypes.Contains(contentType) &&
            !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(new
            {
                success = false,
                error = MapClientError("Unsupported file type.")
            }, statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        try
        {
            await using var stream = file.OpenReadStream();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, context.RequestAborted);
            var bytes = ms.ToArray();

            var userAgent = context.Request.Headers.UserAgent.ToString();
            var deviceName = context.Request.Headers.TryGetValue("X-Device-Name", out var device)
                ? device.ToString()
                : ParseDeviceName(userAgent);

            ObjectId? targetTransactionId;
            string? sessionKey;
            ReceiptImportTarget importTarget;
            lock (_lock)
            {
                targetTransactionId = _session?.TargetTransactionId;
                sessionKey = _session?.SessionKey;
                importTarget = _session?.ImportTarget ?? ReceiptImportTarget.Inbox;
            }

            var item = await _importService.EnqueueFromUploadAsync(
                bytes,
                safeName,
                contentType,
                ReceiptImportSource.Mobile,
                deviceName,
                userAgent,
                pendingTransactionId: targetTransactionId,
                uploadSessionKey: sessionKey,
                importTarget: importTarget,
                cancellationToken: context.RequestAborted);

            _logger.LogInformation(
                "Mobile upload file saved and receipt {ReceiptId} created",
                item.Id);

            lock (_lock)
            {
                if (_session is not null)
                    _session.UploadCount++;
            }

            try { _queue.NotifyItemUpdated(item); }
            catch (Exception notifyEx)
            {
                _logger.LogWarning(notifyEx,
                    "Post-upload notification failed for {FileName}; upload succeeded",
                    safeName);
            }

            _logger.LogInformation(
                "Mobile upload HTTP 200 returned for {FileName} ({Bytes} bytes) from {ClientIp}",
                safeName,
                bytes.Length,
                clientIp);

            return Results.Json(new
            {
                success = true,
                uploadSucceeded = true,
                uploadId = item.Id.ToString(),
                importItemId = item.Id.ToString(),
                target = importTarget.ToString(),
                message = "Receipt uploaded",
                status = "Uploaded",
                statusDisplay = "Receipt uploaded",
                processingStatus = item.Status.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mobile upload failed for {FileName}", safeName);
            return Results.Json(new
            {
                success = false,
                error = "The desktop app encountered an unexpected error."
            }, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private UploadSession CreateSession(int port, MobileUploadSessionRequest? request)
    {
        var ip = GetLocalIPv4() ?? "127.0.0.1";
        return new UploadSession
        {
            Token = GenerateToken(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(TokenLifetimeMinutes),
            AllowedNetworkPrefix = GetNetworkPrefix(ip),
            Port = port,
            LocalIp = ip,
            UploadCount = 0,
            SessionKey = request?.SessionKey ?? Guid.NewGuid().ToString("N"),
            TargetTransactionId = request?.TargetTransactionId,
            ImportTarget = request?.ImportTarget ?? ReceiptImportTarget.Inbox
        };
    }

    private static MobileUploadSessionInfo ToInfo(UploadSession session)
    {
        var url = $"http://{session.LocalIp}:{session.Port}/upload?token={session.Token}";
        return new MobileUploadSessionInfo
        {
            Token = session.Token,
            UploadUrl = url,
            QrPayload = url,
            ExpiresAt = session.ExpiresAt,
            Port = session.Port,
            LocalIpAddress = session.LocalIp,
            SessionKey = session.SessionKey,
            TargetTransactionId = session.TargetTransactionId,
            ImportTarget = session.ImportTarget
        };
    }

    private static string ResolveWwwRootPath() =>
        Path.Combine(AppContext.BaseDirectory, "MobileUpload", "wwwroot");

    private static string ExtractToken(HttpContext context)
    {
        var auth = context.Request.Headers.Authorization.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return auth["Bearer ".Length..].Trim();

        return context.Request.Query["token"].ToString();
    }

    private static string GetClientIp(HttpContext context)
    {
        var remote = context.Connection.RemoteIpAddress;
        if (remote is null)
            return string.Empty;

        if (remote.IsIPv4MappedToIPv6)
            remote = remote.MapToIPv4();

        return remote.ToString();
    }

    private static bool IsAllowedClientIp(string clientIp, string prefix)
    {
        if (string.IsNullOrWhiteSpace(clientIp))
            return false;

        if (clientIp.StartsWith("127.", StringComparison.Ordinal) || clientIp == "::1")
            return true;

        return clientIp.StartsWith(prefix, StringComparison.Ordinal);
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name))
            name = "receipt.jpg";

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
            sb.Append(invalid.Contains(ch) ? '_' : ch);

        var sanitized = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "receipt.jpg" : sanitized;
    }

    private static string GuessContentType(string ext) => ext.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".heic" => "image/heic",
        ".tif" or ".tiff" => "image/tiff",
        _ => "application/octet-stream"
    };

    private static string? ParseDeviceName(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return null;
        if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase)) return "iPhone";
        if (userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase)) return "iPad";
        if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase)) return "Android";
        return "Mobile device";
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return aBytes.Length == bBytes.Length &&
               CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }

    private static string? GetLocalIPv4()
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
                continue;

            if (networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback
                or NetworkInterfaceType.Tunnel)
                continue;

            var props = networkInterface.GetIPProperties();
            if (props.GatewayAddresses.Count == 0 &&
                networkInterface.NetworkInterfaceType != NetworkInterfaceType.Wireless80211)
                continue;

            foreach (var address in props.UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                var ip = address.Address.ToString();
                if (IPAddress.IsLoopback(address.Address))
                    continue;

                if (ip.StartsWith("169.254.", StringComparison.Ordinal))
                    continue;

                return ip;
            }
        }

        foreach (var address in Dns.GetHostAddresses(Dns.GetHostName()))
        {
            if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                return address.ToString();
        }

        return null;
    }

    private static string GetNetworkPrefix(string ip)
    {
        var parts = ip.Split('.');
        return parts.Length >= 3 ? $"{parts[0]}.{parts[1]}.{parts[2]}." : ip;
    }

    private sealed class UploadSession
    {
        public string Token { get; init; } = string.Empty;

        public DateTime CreatedAt { get; init; }

        public DateTime ExpiresAt { get; init; }

        public string AllowedNetworkPrefix { get; init; } = string.Empty;

        public int Port { get; init; }

        public string LocalIp { get; init; } = string.Empty;

        public int UploadCount { get; set; }

        public string? SessionKey { get; init; }

        public ObjectId? TargetTransactionId { get; init; }

        public ReceiptImportTarget ImportTarget { get; init; } = ReceiptImportTarget.Inbox;
    }
}
