namespace NickeltownFinance.Core.Modules;

/// <summary>
/// Extension point for future modules (Memberships, POS, Inventory).
/// Register implementations via DI — the shell discovers them without core rewrites.
/// </summary>
public interface IAppModule
{
    /// <summary>Stable module key, e.g. "memberships".</summary>
    string Key { get; }

    /// <summary>Display name shown in navigation when enabled.</summary>
    string DisplayName { get; }

    /// <summary>Fluent UI symbol name for the nav icon.</summary>
    string IconSymbol { get; }

    /// <summary>Sort order among modules (core nav is fixed; modules append).</summary>
    int SortOrder { get; }

    /// <summary>Whether this module is currently available.</summary>
    bool IsEnabled { get; }
}
