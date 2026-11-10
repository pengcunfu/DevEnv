using Microsoft.Win32;

namespace DevEnv.Core;

public static class AutostartHelper
{
    public const string AppName = "FireNeb DevEnv";

    /// <summary>历史注册表项名，切换显示名时清理，避免重复开机启动。</summary>
    private static readonly string[] LegacyNames = ["燧岚开发盒子", "程序盒子", "ProgramBox"];
    private const string KeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static void Set(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, true);
        if (key is null)
            return;

        if (enable)
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
                return;
            key.SetValue(AppName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(AppName, false);
        }

        foreach (var legacy in LegacyNames)
            key.DeleteValue(legacy, false);
    }

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, false);
        if (key is null)
            return false;

        return key.GetValue(AppName) != null
            || LegacyNames.Any(name => key.GetValue(name) != null);
    }

    /// <summary>
    /// 把旧显示名的 Run 项迁到当前 <see cref="AppName"/>，避免更名后重复启动或条目残留。
    /// </summary>
    public static void MigrateLegacyNames()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, true);
        if (key is null)
            return;

        var current = key.GetValue(AppName) as string;
        foreach (var legacy in LegacyNames)
        {
            var value = key.GetValue(legacy) as string;
            if (value is null)
                continue;

            if (string.IsNullOrEmpty(current))
            {
                key.SetValue(AppName, value);
                current = value;
            }

            key.DeleteValue(legacy, false);
        }
    }
}
