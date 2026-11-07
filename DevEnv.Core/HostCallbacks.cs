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
    /// <summary>显示并激活主窗口（从托盘、快捷应用等入口唤回）。</summary>
    public static Action? OnShowMainWindow { get; set; }
    /// <summary>显示并激活开发窗口（开发环境 / 熔岩环境），供启动器内置工具入口使用。</summary>
    public static Action? OnShowDevelopmentWindow { get; set; }
    /// <summary>显示并激活监控窗口（网络 / 进程 / 服务监控），供启动器内置工具入口使用。</summary>
    public static Action? OnShowMonitorWindow { get; set; }
    public static Action? OnBeforeExitForUpdate { get; set; }
    public static Action? OnExitApp { get; set; }
    /// <summary>data.json 中的全局快捷键或 Win 键开关变化，宿主重新读取并注册。</summary>
    public static Action? OnGlobalHotkeysChanged { get; set; }
    public static Action<string>? SetClipboardText { get; set; }
    public static Action<string>? SetClipboardImagePath { get; set; }
    public static Action<bool>? RestartElevated { get; set; }

    // SigXor 模块（文件快速预览 / 语音输入 / 区域截图 OCR 取色）
    /// <summary>打开文件快速预览（选择文件后弹预览浮窗）。</summary>
    public static Action? OnOpenFilePreview { get; set; }
    /// <summary>开始区域截图（选区 → OCR / 取色 / 保存）。</summary>
    public static Action? OnOpenScreenshot { get; set; }
    /// <summary>打开模块设置窗口的指定页签（常规 / 语音与截屏 / 服务与模型）。</summary>
    public static Action<SettingsSection>? OnOpenSigXorSettings { get; set; }
    /// <summary>打开快捷应用（搜索并启动应用 / 文件 / 命令）。</summary>
    public static Action? OnOpenSigXorLauncher { get; set; }
    /// <summary>打开文件浏览器窗口（目录导航 + 内嵌预览）。</summary>
    public static Action? OnOpenSigXorBrowser { get; set; }
    /// <summary>打开模块更新窗口。</summary>
    public static Action? OnOpenSigXorUpdate { get; set; }
}
