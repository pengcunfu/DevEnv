using Forms = System.Windows.Forms;

namespace DevEnv.Host.Infrastructure;

/// <summary>
/// 托盘图标。WPF 无内置托盘控件，这里是全项目唯一使用 WinForms UI 类型的运行时组件
/// （System.Windows.Forms.NotifyIcon），因此 Host 同时开启 UseWPF 与 UseWindowsForms。
/// </summary>
internal sealed class TrayIconHost : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _menu = new();

    public event Action? ShowWindowRequested;
    public event Action? TranslatorRequested;
    public event Action? ClipboardRequested;
    public event Action? ExitRequested;

    /// <summary>SigXor 模块：打开文件快速预览。</summary>
    public event Action? FilePreviewRequested;
    /// <summary>SigXor 模块：开始区域截图。</summary>
    public event Action? ScreenshotRequested;
    /// <summary>SigXor 模块：启动语音服务。</summary>
    public event Action? StartVoiceRequested;
    /// <summary>SigXor 模块：停止语音服务。</summary>
    public event Action? StopVoiceRequested;
    /// <summary>SigXor 模块：打开模块设置。</summary>
    public event Action? SigXorSettingsRequested;
    /// <summary>SigXor 模块：打开全局启动器。</summary>
    public event Action? SigXorLauncherRequested;
    /// <summary>SigXor 模块：打开文件浏览器窗口。</summary>
    public event Action? SigXorBrowserRequested;
    /// <summary>SigXor 模块：打开模块关于窗口。</summary>
    public event Action? SigXorAboutRequested;

    public TrayIconHost(string text, System.Drawing.Icon? icon)
    {
        _notifyIcon = new Forms.NotifyIcon
        {
            Text = text,
            Icon = icon ?? System.Drawing.SystemIcons.Application,
            Visible = true,
        };

        _menu.Items.Add("显示主窗口", null, (_, _) => Raise(ShowWindowRequested));
        _menu.Items.Add("熔岩翻译", null, (_, _) => Raise(TranslatorRequested));
        _menu.Items.Add("熔岩剪贴板", null, (_, _) => Raise(ClipboardRequested));
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("文件快速预览…", null, (_, _) => Raise(FilePreviewRequested));
        _menu.Items.Add("全局启动器…", null, (_, _) => Raise(SigXorLauncherRequested));
        _menu.Items.Add("文件浏览器…", null, (_, _) => Raise(SigXorBrowserRequested));
        _menu.Items.Add("区域截图", null, (_, _) => Raise(ScreenshotRequested));
        _menu.Items.Add("启动语音服务", null, (_, _) => Raise(StartVoiceRequested));
        _menu.Items.Add("停止语音服务", null, (_, _) => Raise(StopVoiceRequested));
        _menu.Items.Add("文件预览与语音设置…", null, (_, _) => Raise(SigXorSettingsRequested));
        _menu.Items.Add("关于文件预览模块…", null, (_, _) => Raise(SigXorAboutRequested));
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("退出", null, (_, _) => Raise(ExitRequested));
        _notifyIcon.ContextMenuStrip = _menu;
        _notifyIcon.DoubleClick += (_, _) => Raise(ShowWindowRequested);
    }

    /// <summary>语音服务运行时，把「启动语音服务」置灰，只留「停止」。</summary>
    public void SetVoiceServiceState(bool running)
    {
        UpdateMenuItem("启动语音服务", !running);
        UpdateMenuItem("停止语音服务", running);
    }

    private void UpdateMenuItem(string text, bool visible)
    {
        if (_menu.Items[text] is { } item)
            item.Visible = visible;
    }

    public bool Visible
    {
        get => _notifyIcon.Visible;
        set => _notifyIcon.Visible = value;
    }

    public void ShowBalloon(string message, Forms.ToolTipIcon icon = Forms.ToolTipIcon.Info)
    {
        if (!_notifyIcon.Visible)
            return;
        try
        {
            _notifyIcon.ShowBalloonTip(2000, "燧岚开发盒子", message, icon);
        }
        catch
        {
            // 图标已被释放时忽略
        }
    }

    private static void Raise(Action? handler)
    {
        try { handler?.Invoke(); }
        catch
        {
            // 托盘事件不能把异常抛回 WinForms 消息循环
        }
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
    }
}