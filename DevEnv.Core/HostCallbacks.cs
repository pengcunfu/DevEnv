namespace DevEnv.Core;

/// <summary>由 WPF 宿主注入的 UI 回调（文件对话框、托盘、翻译窗、剪贴板写入等）。</summary>
public static class HostCallbacks
{
    public static Func<string?>? PickExecutable { get; set; }
    public static Func<string?>? PickFolder { get; set; }
    public static Func<string, string[]?>? PickFiles { get; set; }
    public static Func<string, string, string?>? SaveFile { get; set; }
    public static Action<bool>? OnTrayIconChanged { get; set; }
    public static Action<string?>? OnActivateTab { get; set; }
    public static Action<TranslatorRequest>? OnShowTranslator { get; set; }
    public static Action<bool>? OnShowClipboard { get; set; }
    public static Action? OnBeforeExitForUpdate { get; set; }
    public static Action? OnExitApp { get; set; }
    public static Action<string?>? OnShowWindowHotkeyChanged { get; set; }
    public static Action<string>? SetClipboardText { get; set; }
    public static Action<string>? SetClipboardImagePath { get; set; }
    public static Action<bool>? RestartElevated { get; set; }
}
