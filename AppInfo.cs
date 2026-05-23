using System.Reflection;

namespace DevEnv;

/// <summary>应用版本信息，与 DevEnv.csproj 中的 Version 同步。</summary>
public static class AppInfo
{
    public static string Version
    {
        get
        {
            var informational = typeof(AppInfo).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informational))
                return informational.Split('+')[0];

            return typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }
    }
}
