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
            DoubleClickToLaunch = json.Value<bool?>("DoubleClickToLaunch") == true,
            ShowWindowHotkey = json.Value<string>("ShowWindowHotkey") ?? "Ctrl+Alt+D",
            // 缺省开启：新用户装上就能用 Win 键唤出全局启动器
            WinKeyLauncher = json.Value<bool?>("WinKeyLauncher") != false,
        };
    }

    /// <summary>保存并派发副作用：开机自启 / 托盘图标 / 显示窗口热键。</summary>
    public OperationResult Save(AppConfig config)
    {
        var patch = new Newtonsoft.Json.Linq.JObject
        {
            ["TrayIcon"] = config.TrayIcon,
            ["SelfStarting"] = config.SelfStarting,
            ["BaseEnvPath"] = config.BaseEnvPath,
            ["Language"] = config.Language,
            ["DarkMode"] = config.DarkMode,
            ["DoubleClickToLaunch"] = config.DoubleClickToLaunch,
            ["ShowWindowHotkey"] = config.ShowWindowHotkey,
            ["WinKeyLauncher"] = config.WinKeyLauncher,
        };

        var saved = _store.SaveConfig(patch);
        if (saved["SelfStarting"] is not null)
            AutostartHelper.Set(saved.Value<bool?>("SelfStarting") == true);
        if (saved["TrayIcon"] is not null)
            HostCallbacks.OnTrayIconChanged?.Invoke(saved.Value<bool?>("TrayIcon") != false);
        if (saved["ShowWindowHotkey"] is not null)
            HostCallbacks.OnShowWindowHotkeyChanged?.Invoke(saved.Value<string>("ShowWindowHotkey"));
        if (saved["WinKeyLauncher"] is not null)
            HostCallbacks.OnWinKeyLauncherChanged?.Invoke(saved.Value<bool?>("WinKeyLauncher") != false);

        return OperationResult.Success("已保存");
    }
}