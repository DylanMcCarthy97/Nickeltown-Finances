using System.Reflection;

namespace NickeltownFinance.Core;

public static class AppInfo
{
    public static string Version { get; } = NormalizeDisplayVersion(
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0");

    public static string VersionLabel => $"v{Version}";

    private static string NormalizeDisplayVersion(string raw)
    {
        var plusIndex = raw.IndexOf('+');
        return plusIndex >= 0 ? raw[..plusIndex] : raw;
    }
}
