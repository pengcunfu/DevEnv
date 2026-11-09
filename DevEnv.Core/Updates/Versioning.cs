using System.Reflection;

namespace DevEnv.Core.Updates;

/// <summary>程序版本（从入口程序集读取，避免 csproj 与代码版本不一致）。</summary>
public static class AppVersionInfo
{
    public static Version Current { get; } = Resolve();

    public static string Display => AppVersions.Display(Current);

    private static Version Resolve()
    {
        Version? entry = Assembly.GetEntryAssembly()?.GetName().Version;
        if (entry is not null)
            return entry;

        return typeof(AppVersionInfo).Assembly.GetName().Version ?? new Version(0, 0, 0);
    }
}

/// <summary>版本号解析与比较。支持 <c>1.2.3</c>、<c>1.2.3.0</c> 与 <c>1.2.3-beta</c>（按 1.2.3 比较）。</summary>
public static class AppVersions
{
    public static bool TryParse(string? text, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string value = text.Trim();
        if (value.StartsWith('v') || value.StartsWith('V'))
            value = value[1..];

        int suffix = value.IndexOfAny(['-', '+']);
        if (suffix >= 0)
            value = value[..suffix];

        return Version.TryParse(value, out version!);
    }

    public static string Display(Version version)
    {
        int build = version.Build < 0 ? 0 : version.Build;
        return $"{version.Major}.{version.Minor}.{build}";
    }

    /// <summary>组件清单只要求主版本 / 次版本一致，补丁号可以不同。</summary>
    public static bool IsCompatible(string? required, Version current)
        => TryParse(required, out Version parsed)
           && parsed.Major == current.Major
           && parsed.Minor == current.Minor;

    public static bool IsNewer(Version candidate, Version current) => candidate > current;
}
