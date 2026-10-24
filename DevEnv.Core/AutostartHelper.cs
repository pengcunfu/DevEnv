using Microsoft.Win32;

namespace DevEnv.Core;

public static class AutostartHelper
{
    public const string AppName = "燧岚开发盒子";

    /// <summary>历史注册表项名，切换显示名时清理，避免重复开机启动。</summary>
    private static readonly string[] LegacyNames = ["程序盒子", "ProgramBox"];
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
}
