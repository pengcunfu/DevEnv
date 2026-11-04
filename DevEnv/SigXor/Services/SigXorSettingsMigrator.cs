using System.IO;
using System.Text.Json;
using DevEnv.Core.SigXor.Toolkit;

namespace DevEnv.Host.SigXor.Services;

public static class SigXorSettingsMigrator
{
    private static string LegacyConfigPath =>
        Path.Combine(AppPaths.LegacySigXorDataDirectory, "config.json");

    public static bool TryMigrate(AppSettings settings)
    {
        if (settings.SigXorMigrated) return false;
        if (!File.Exists(LegacyConfigPath)) return false;

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(LegacyConfigPath));
            JsonElement root = document.RootElement;

            settings.Voice.Shortcut = ShortcutDefinition.NormalizeVoiceShortcut(
                ReadString(root, "voiceShortcut") ?? settings.Voice.Shortcut);
            settings.Voice.Language = NormalizeLanguage(
                ReadString(root, "recognitionLanguage") ?? settings.Voice.Language);
            settings.Voice.Engine = "sensevoice";
            settings.Voice.HoldThresholdSeconds = ReadDouble(root, "altHoldThreshold")
                ?? settings.Voice.HoldThresholdSeconds;
            settings.Voice.TypingDelay = ReadDouble(root, "typingDelay")
                ?? settings.Voice.TypingDelay;
            settings.Voice.SampleRate = ReadInt(root, "sampleRate")
                ?? settings.Voice.SampleRate;
            settings.Voice.Channels = ReadInt(root, "channels")
                ?? settings.Voice.Channels;
            settings.Voice.BitDepth = ReadInt(root, "bitDepth")
                ?? settings.Voice.BitDepth;
            settings.Voice.UseClipboard = ReadBool(root, "useClipboard")
                ?? settings.Voice.UseClipboard;
            settings.Voice.DebugMode = ReadBool(root, "debugMode")
                ?? settings.Voice.DebugMode;
            settings.Voice.SaveAudioFiles = ReadBool(root, "saveAudioFiles")
                ?? settings.Voice.SaveAudioFiles;

            settings.Capture.Enabled = ReadBool(root, "enableScreenshotShortcut")
                ?? settings.Capture.Enabled;
            settings.Capture.Modifier = ShortcutDefinition.NormalizeCaptureModifier(
                ReadString(root, "screenshotShortcut") ?? settings.Capture.Modifier);

            settings.Ui.ShowNotifications = ReadBool(root, "showNotifications")
                ?? settings.Ui.ShowNotifications;
            settings.Ui.MinimizeToTray = ReadBool(root, "minimizeToTray")
                ?? settings.Ui.MinimizeToTray;
            settings.Ui.SilentStart = ReadBool(root, "silentStart")
                ?? settings.Ui.SilentStart;

            settings.AutoStartEnabled = ReadBool(root, "autoStartWithWindows")
                ?? settings.AutoStartEnabled;
            settings.CheckUpdatesOnStartup = ReadBool(root, "autoCheckUpdate")
                ?? settings.CheckUpdatesOnStartup;

            settings.SigXorMigrated = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeLanguage(string value) =>
        value is "auto" or "zh-CN" or "en-US" or "yue" or "ja" or "ko" ? value : "auto";

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool? ReadBool(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            }
            : null;

    private static double? ReadDouble(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out double result)
            ? result
            : null;

    private static int? ReadInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out int result)
            ? result
            : null;
}
