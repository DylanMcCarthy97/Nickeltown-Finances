namespace NickeltownFinance.Core.Update;

/// <summary>
/// GitHub release feed used for update checks. Set these to your repository before publishing.
/// </summary>
public static class UpdateConstants
{
    public const string GitHubOwner = "DylanMcCarthy97";
    public const string GitHubRepo = "Nickeltown-Finances";
    public const string UserAgent = "NickeltownFinance-Updater";

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(GitHubOwner)
        && !string.Equals(GitHubOwner, "YOUR_GITHUB_USER", StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(GitHubRepo);
}
