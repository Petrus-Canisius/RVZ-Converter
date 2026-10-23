using System.Runtime.InteropServices;

namespace RvzConverter.Core;

public enum DesktopPlatform { Windows, Linux, MacOS }
public sealed record AppDirectories(string Preferences, string Cache, string Logs);

public static class PlatformPaths
{
    public static DesktopPlatform Current => OperatingSystem.IsWindows() ? DesktopPlatform.Windows
        : OperatingSystem.IsMacOS() ? DesktopPlatform.MacOS : DesktopPlatform.Linux;
    public static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    public static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public static AppDirectories Directories => For(Current, Home, Environment.GetEnvironmentVariable);
    public static string RuntimeId => $"{(Current == DesktopPlatform.Windows ? "win" : Current == DesktopPlatform.MacOS ? "osx" : "linux")}-{RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}";
    public static AppDirectories For(DesktopPlatform platform, string home, Func<string, string?> environment)
    {
        string Base(string variable, string fallback) => environment(variable) is { Length: > 0 } value && Path.IsPathFullyQualified(value) ? value : fallback;
        return platform switch
        {
            DesktopPlatform.Windows => new(Path.Combine(Base("LOCALAPPDATA", Path.Combine(home, "AppData", "Local")), "RVZConverter"),
                Path.Combine(Base("LOCALAPPDATA", Path.Combine(home, "AppData", "Local")), "RVZConverter", "cache"),
                Path.Combine(Base("LOCALAPPDATA", Path.Combine(home, "AppData", "Local")), "RVZConverter", "logs")),
            DesktopPlatform.MacOS => new(Path.Combine(home, "Library", "Application Support", "RVZConverter"),
                Path.Combine(home, "Library", "Caches", "RVZConverter"), Path.Combine(home, "Library", "Logs", "RVZConverter")),
            _ => new(Path.Combine(Base("XDG_CONFIG_HOME", Path.Combine(home, ".config")), "RVZConverter"),
                Path.Combine(Base("XDG_CACHE_HOME", Path.Combine(home, ".cache")), "RVZConverter"),
                Path.Combine(Base("XDG_STATE_HOME", Path.Combine(home, ".local", "state")), "RVZConverter", "logs"))
        };
    }
}
