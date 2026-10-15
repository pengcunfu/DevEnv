using System.IO;
using System.Text.Json;
using NetworkWatch.Models;

namespace NetworkWatch.Services;

/// <summary>
/// 浅色/深色偏好持久化（嵌入 DevBox 后仅记录偏好，UI 用本地资源切换）。
/// </summary>
public static class ThemeService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetworkWatch");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    public static AppTheme Current { get; private set; } = AppTheme.Light;

    public static AppTheme LoadSaved()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(SettingsPath));
                if (doc.RootElement.TryGetProperty("theme", out var value) &&
                    Enum.TryParse<AppTheme>(value.GetString(), ignoreCase: true, out var theme))
                {
                    Current = theme;
                    return theme;
                }
            }
        }
        catch
        {
            // ignore
        }

        Current = AppTheme.Light;
        return Current;
    }

    public static void Save(AppTheme theme)
    {
        Current = theme;
        try
        {
            Directory.CreateDirectory(SettingsDir);
            File.WriteAllText(SettingsPath, $"{{\"theme\":\"{theme}\"}}");
        }
        catch
        {
            // ignore
        }
    }

    public static AppTheme Toggle()
    {
        var next = Current == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
        Save(next);
        return next;
    }
}
