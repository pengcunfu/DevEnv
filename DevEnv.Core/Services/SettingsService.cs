using DevEnv.Core.Input;
using DevEnv.Core.Models;

namespace DevEnv.Core.Services;

/// <summary>主程序设置（对应 data.json 的 Config 段），带强类型读写与副作用派发。</summary>
public sealed class SettingsService
{
    private readonly ConfigStore _store;

    public SettingsService(ConfigStore store) => _store = store;

    public AppConfig GetConfig()
    {
        var json = _store.GetConfig();
        return new AppConfig
        {
            TrayIcon = json.Value<bool?>("TrayIcon") != false,
            SelfStarting = json.Value<bool?>("SelfStarting") == true,
            BaseEnvPath = json.Value<string>("BaseEnvPath") ?? string.Empty,
            Language = json.Value<string>("Language") ?? "zh-CN",
            DarkMode = json.Value<bool?>("DarkMode") == true,
            ShowWindowHotkey = json.Value<string>("ShowWindowHotkey") ?? "Ctrl+Alt+D",
            ClipboardHotkey = json.Value<string>("ClipboardHotkey") ?? "Ctrl+Space",
            TranslatorHotkey = json.Value<string>("TranslatorHotkey") ?? "Alt+Space",
            // 缺省开启：新用户装上就能用左 Alt 键唤出全局启动器
            AltKeyLauncher = json.Value<bool?>("AltKeyLauncher") != false,
        };
    }

    /// <summary>
    /// 保存常规页拥有的字段（托盘 / 自启 / 环境目录 / 语言 / 主题）。
    /// 不写快捷键字段，避免常规页用打开设置时的旧值覆盖「快捷键」页刚保存的值。
    /// </summary>
    public OperationResult SaveGeneral(AppConfig config)
    {
        var patch = new Newtonsoft.Json.Linq.JObject
        {
            ["TrayIcon"] = config.TrayIcon,
            ["SelfStarting"] = config.SelfStarting,
            ["BaseEnvPath"] = config.BaseEnvPath,
            ["Language"] = config.Language,
            ["DarkMode"] = config.DarkMode,
        };

        var saved = _store.SaveConfig(patch);
        if (saved["SelfStarting"] is not null)
            AutostartHelper.Set(saved.Value<bool?>("SelfStarting") == true);
        if (saved["TrayIcon"] is not null)
            HostCallbacks.OnTrayIconChanged?.Invoke(saved.Value<bool?>("TrayIcon") != false);

        return OperationResult.Success("已保存");
    }

    /// <summary>保存全局快捷键与 Win 键开关，并通知宿主重新注册热键。</summary>
    public OperationResult SaveHotkeys(
        string showWindowHotkey,
        string clipboardHotkey,
        string translatorHotkey,
        bool altKeyLauncher)
    {
        var errors = HotkeyValidator.ValidateBindings(
        [
            ("显示主窗口", showWindowHotkey),
            ("熔岩剪贴板", clipboardHotkey),
            ("熔岩翻译", translatorHotkey),
        ]);
        if (errors.Count > 0)
            return OperationResult.Fail(errors[0].Message);

        HotkeyParser.TryParse(showWindowHotkey, out HotkeyGesture show);
        HotkeyParser.TryParse(clipboardHotkey, out HotkeyGesture clipboard);
        HotkeyParser.TryParse(translatorHotkey, out HotkeyGesture translator);

        var patch = new Newtonsoft.Json.Linq.JObject
        {
            ["ShowWindowHotkey"] = HotkeyParser.FormatCanonical(show),
            ["ClipboardHotkey"] = HotkeyParser.FormatCanonical(clipboard),
            ["TranslatorHotkey"] = HotkeyParser.FormatCanonical(translator),
            ["AltKeyLauncher"] = altKeyLauncher,
        };

        _store.SaveConfig(patch);
        HostCallbacks.OnGlobalHotkeysChanged?.Invoke();
        return OperationResult.Success("已保存");
    }
}