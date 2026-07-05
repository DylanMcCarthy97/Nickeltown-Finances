using System.Reflection;

namespace NickeltownFinance.Core;

public static class AppInfo
{
    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

    public static string VersionLabel => $"v{Version}";
}
