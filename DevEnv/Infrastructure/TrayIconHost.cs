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
        _menu.Items.Add("退出", null, (_, _) => Raise(ExitRequested));
        _notifyIcon.ContextMenuStrip = _menu;
        _notifyIcon.DoubleClick += (_, _) => Raise(ShowWindowRequested);
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
            _notifyIcon.ShowBalloonTip(2000, "程序盒子", message, icon);
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