using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using BCrypt.Net;

var builder = WebApplication.CreateBuilder(args);

var dataRoot = builder.Configuration["SyncDataRoot"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "data");
Directory.CreateDirectory(dataRoot);

builder.Services.AddSingleton(new SyncStorage(dataRoot));

var app = builder.Build();

app.MapGet("/", () => Results.Text("Nickeltown Finance Sync Server"));

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/v1/clubs/{clubId}/status", (string clubId, HttpRequest request, SyncStorage storage) =>
{
    if (!TryAuthorize(clubId, request, storage, out var error, out _))
        return error!;

    var manifest = storage.ReadManifest(clubId);
    if (manifest is null)
        return Results.Ok(new ClubStatusResponse(Exists: true, HasPackage: false, Manifest: null));

    return Results.Ok(new ClubStatusResponse(Exists: true, HasPackage: true, Manifest: manifest));
});

app.MapGet("/v1/clubs/{clubId}/package", (string clubId, HttpRequest request, SyncStorage storage) =>
{
    if (!TryAuthorize(clubId, request, storage, out var error, out _))
        return error!;

    var packagePath = storage.PackagePath(clubId);
    var manifest = storage.ReadManifest(clubId);
    if (manifest is null || !File.Exists(packagePath))
        return Results.NotFound(new { error = "No club package has been published yet." });

    return Results.File(packagePath, "application/octet-stream", "nickeltown_club_sync.bin");
});

app.MapPut("/v1/clubs/{clubId}/package", async (string clubId, HttpRequest request, SyncStorage storage) =>
{
    if (!ClubId.IsValid(clubId))
        return Results.BadRequest(new { error = "Club id must be 3–64 characters: lowercase letters, numbers, and hyphens." });

    var syncCode = ReadSyncCode(request);
    if (string.IsNullOrWhiteSpace(syncCode) || syncCode.Length < 8)
        return Results.BadRequest(new { error = "Sync code must be at least 8 characters." });

    if (!storage.TryEnsureClub(clubId, syncCode, out var authError))
        return Results.Json(new { error = authError }, statusCode: StatusCodes.Status401Unauthorized);

    if (!long.TryParse(request.Headers["X-Base-Revision"].FirstOrDefault(), out var baseRevision))
        baseRevision = 0;

    var force = string.Equals(request.Headers["X-Force"].FirstOrDefault(), "true", StringComparison.OrdinalIgnoreCase);
    var current = storage.ReadManifest(clubId);
    if (!force && current is not null && current.Revision != baseRevision)
    {
        return Results.Json(
            new { error = "Conflict: the server has a newer revision.", remote = current },
            statusCode: StatusCodes.Status409Conflict);
    }

    if (!long.TryParse(request.Headers["X-Revision"].FirstOrDefault(), out var nextRevision) || nextRevision <= 0)
        nextRevision = (current?.Revision ?? 0) + 1;

    var deviceId = request.Headers["X-Device-Id"].FirstOrDefault() ?? "unknown";
    var machineName = request.Headers["X-Machine-Name"].FirstOrDefault() ?? "unknown";

    var tempPath = storage.PackagePath(clubId) + ".tmp";
    await using (var output = File.Create(tempPath))
    {
        await request.Body.CopyToAsync(output);
    }

    var info = new FileInfo(tempPath);
    if (info.Length <= 0)
    {
        File.Delete(tempPath);
        return Results.BadRequest(new { error = "Empty package." });
    }

    var finalPath = storage.PackagePath(clubId);
    if (File.Exists(finalPath))
        File.Delete(finalPath);
    File.Move(tempPath, finalPath);

    var manifest = new SyncManifest(
        Revision: nextRevision,
        WrittenUtc: DateTime.UtcNow,
        DeviceId: deviceId,
        MachineName: machineName,
        PackageBytes: info.Length);

    storage.WriteManifest(clubId, manifest);
    return Results.Ok(manifest);
});

app.Run();

static bool TryAuthorize(
    string clubId,
    HttpRequest request,
    SyncStorage storage,
    out IResult? error,
    out string? syncCode)
{
    error = null;
    syncCode = ReadSyncCode(request);

    if (!ClubId.IsValid(clubId))
    {
        error = Results.BadRequest(new { error = "Invalid club id." });
        return false;
    }

    if (string.IsNullOrWhiteSpace(syncCode))
    {
        error = Results.Json(new { error = "Missing sync code." }, statusCode: StatusCodes.Status401Unauthorized);
        return false;
    }

    if (!storage.ClubExists(clubId))
    {
        error = Results.NotFound(new { error = "Unknown club id. Push from the first computer to create it." });
        return false;
    }

    if (!storage.VerifySyncCode(clubId, syncCode))
    {
        error = Results.Json(new { error = "Incorrect sync code." }, statusCode: StatusCodes.Status401Unauthorized);
        return false;
    }

    return true;
}

static string? ReadSyncCode(HttpRequest request) =>
    request.Headers["X-Club-Sync-Code"].FirstOrDefault()
    ?? request.Query["syncCode"].FirstOrDefault();

internal static class ClubId
{
    private static readonly Regex Pattern = new("^[a-z0-9]([a-z0-9-]{1,62}[a-z0-9])$", RegexOptions.Compiled);

    public static bool IsValid(string clubId) =>
        !string.IsNullOrWhiteSpace(clubId) && Pattern.IsMatch(clubId);
}

internal sealed class SyncStorage(string dataRoot)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public bool ClubExists(string clubId) => Directory.Exists(ClubDir(clubId));

    public string PackagePath(string clubId) => Path.Combine(ClubDir(clubId), "package.bin");

    public bool TryEnsureClub(string clubId, string syncCode, out string? error)
    {
        error = null;
        var dir = ClubDir(clubId);
        var hashPath = Path.Combine(dir, "sync-code.hash");

        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(hashPath, BCrypt.Net.BCrypt.HashPassword(syncCode));
            return true;
        }

        if (!File.Exists(hashPath))
        {
            File.WriteAllText(hashPath, BCrypt.Net.BCrypt.HashPassword(syncCode));
            return true;
        }

        var hash = File.ReadAllText(hashPath);
        if (!BCrypt.Net.BCrypt.Verify(syncCode, hash))
        {
            error = "Incorrect sync code.";
            return false;
        }

        return true;
    }

    public bool VerifySyncCode(string clubId, string syncCode)
    {
        var hashPath = Path.Combine(ClubDir(clubId), "sync-code.hash");
        if (!File.Exists(hashPath))
            return false;
        return BCrypt.Net.BCrypt.Verify(syncCode, File.ReadAllText(hashPath));
    }

    public SyncManifest? ReadManifest(string clubId)
    {
        var path = Path.Combine(ClubDir(clubId), "manifest.json");
        if (!File.Exists(path))
            return null;
        return JsonSerializer.Deserialize<SyncManifest>(File.ReadAllText(path), JsonOptions);
    }

    public void WriteManifest(string clubId, SyncManifest manifest)
    {
        Directory.CreateDirectory(ClubDir(clubId));
        var path = Path.Combine(ClubDir(clubId), "manifest.json");
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(manifest, JsonOptions));
        File.Copy(temp, path, overwrite: true);
        File.Delete(temp);
    }

    private string ClubDir(string clubId) => Path.Combine(dataRoot, clubId);
}

internal sealed record SyncManifest(
    long Revision,
    DateTime WrittenUtc,
    string DeviceId,
    string MachineName,
    long PackageBytes);

internal sealed record ClubStatusResponse(bool Exists, bool HasPackage, SyncManifest? Manifest);
