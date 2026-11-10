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

    /// <summary>右键菜单「显示主窗口」与单击托盘：打开启动器（主窗口）。</summary>
    public event Action? MainWindowRequested;
    /// <summary>右键菜单「设置…」：打开合并后的设置对话框（含关于与帮助）。</summary>
    public event Action? SettingsRequested;
    public event Action? TranslatorRequested;
    public event Action? ClipboardRequested;
    public event Action? ExitRequested;

    /// <summary>SigXor 模块：打开文件快速预览。</summary>
    public event Action? FilePreviewRequested;
    /// <summary>SigXor 模块：开始区域截图。</summary>
    public event Action? ScreenshotRequested;
    /// <summary>SigXor 模块：打开文件浏览器窗口。</summary>
    public event Action? SigXorBrowserRequested;

    public TrayIconHost(string text, System.Drawing.Icon? icon)
    {
        _notifyIcon = new Forms.NotifyIcon
        {
            Text = text,
            Icon = icon ?? System.Drawing.SystemIcons.Application,
            Visible = true,
        };

        _menu.Items.Add("显示主窗口", null, (_, _) => Raise(MainWindowRequested));
        _menu.Items.Add("设置…", null, (_, _) => Raise(SettingsRequested));
        _menu.Items.Add("熔岩翻译", null, (_, _) => Raise(TranslatorRequested));
        _menu.Items.Add("熔岩剪贴板", null, (_, _) => Raise(ClipboardRequested));
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("文件快速预览…", null, (_, _) => Raise(FilePreviewRequested));
        _menu.Items.Add("文件浏览器…", null, (_, _) => Raise(SigXorBrowserRequested));
        _menu.Items.Add("区域截图", null, (_, _) => Raise(ScreenshotRequested));
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("退出", null, (_, _) => Raise(ExitRequested));
        _notifyIcon.ContextMenuStrip = _menu;

        // 单击托盘图标 = 显示主窗口（启动器）。
        // 右键仍然弹出菜单（NotifyIcon 挂上 ContextMenuStrip 后的默认行为）。
        _notifyIcon.MouseClick += OnMouseClick;
    }

    private void OnMouseClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button != Forms.MouseButtons.Left)
            return;
        Raise(MainWindowRequested);
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
            _notifyIcon.ShowBalloonTip(2000, "FireNeb DevEnv", message, icon);
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
