using Microsoft.Win32;

namespace DevEnv.Core;

public static class AutostartHelper
{
    public const string AppName = "程序盒子";
    private const string LegacyName = "ProgramBox";
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

        key.DeleteValue(LegacyName, false);
    }

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, false);
        return key?.GetValue(AppName) != null || key?.GetValue(LegacyName) != null;
    }
}
