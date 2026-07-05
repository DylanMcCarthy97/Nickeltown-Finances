using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Update;

namespace NickeltownFinance.Infrastructure.Services;

public sealed class GitHubReleaseClient : IGitHubReleaseClient, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubReleaseClient> _logger;

    public GitHubReleaseClient(ILogger<GitHubReleaseClient> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UpdateConstants.UserAgent, "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<AppUpdateInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        if (!UpdateConstants.IsConfigured)
        {
            _logger.LogWarning("GitHub update feed is not configured.");
            return null;
        }

        var url = $"https://api.github.com/repos/{UpdateConstants.GitHubOwner}/{UpdateConstants.GitHubRepo}/releases/latest";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GitHub release check failed with status {StatusCode}", response.StatusCode);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<GitHubReleaseDto>(stream, JsonOptions, cancellationToken);
        if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            return null;

        var version = AppUpdateVersion.Normalize(release.TagName);
        if (version is null)
            return null;

        var msixUrl = release.Assets?
            .OrderByDescending(a => a.Name.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(a => a.Name.EndsWith(".msix", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(a =>
                a.Name.EndsWith(".msix", StringComparison.OrdinalIgnoreCase)
                || a.Name.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase))
            ?.BrowserDownloadUrl;

        return new AppUpdateInfo(
            version,
            release.TagName.Trim(),
            release.Body?.Trim() ?? string.Empty,
            msixUrl,
            release.HtmlUrl ?? url,
            release.PublishedAt ?? DateTimeOffset.UtcNow);
    }

    public void Dispose() => _httpClient.Dispose();

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAssetDto>? Assets { get; set; }
    }

    private sealed class GitHubAssetDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}
