using System.IO;
using System.Text.Json;
using DevEnv.Core.SigXor.Toolkit;

namespace DevEnv.Host.SigXor.Services;

/// <summary>设置读写服务。</summary>
public sealed class SettingsService
{
    private readonly object _lock = new();
    private AppSettings? _current;

    public AppSettings Current
    {
        get
        {
            lock (_lock)
            {
                if (_current is not null) return _current;
                try
                {
                    if (File.Exists(AppPaths.SettingsPath))
                    {
                        string json = File.ReadAllText(AppPaths.SettingsPath);
                        _current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    }
                }
                catch
                {
                    // 设置损坏时回退默认值
                }
                _current ??= new AppSettings();
                Normalize(_current);
                return _current;
            }
        }
    }

    private static void Normalize(AppSettings settings)
    {
        settings.Voice ??= new VoiceSettings();
        settings.Capture ??= new CaptureSettings();
        settings.Preview ??= new PreviewSettings();
        settings.Ui ??= new UiSettings();

        settings.Voice.Shortcut = ShortcutDefinition.NormalizeVoiceShortcut(settings.Voice.Shortcut);
        settings.Capture.Modifier = ShortcutDefinition.NormalizeCaptureModifier(settings.Capture.Modifier);
        settings.Preview.OpenShortcut = ShortcutDefinition.NormalizePreviewShortcut(
            settings.Preview.OpenShortcut, ShortcutDefinition.PreviewSpace);
        settings.Preview.CloseShortcut = ShortcutDefinition.NormalizePreviewShortcut(
            settings.Preview.CloseShortcut, ShortcutDefinition.PreviewEscape);
    }

    public void Save()
    {
        lock (_lock)
        {
            if (_current is null) return;
            AppPaths.EnsureCreated();
            string json = JsonSerializer.Serialize(_current, new JsonSerializerOptions { WriteIndented = true });
            string temp = AppPaths.SettingsPath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, AppPaths.SettingsPath, overwrite: true);
        }
    }
}
