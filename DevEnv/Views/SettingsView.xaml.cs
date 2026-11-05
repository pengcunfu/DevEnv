using System.Windows.Controls;

namespace DevEnv.Host.Views;

/// <summary>
/// 设置页。轮询的释放交给宿主窗口关闭时做，不在 Unloaded 时停：
/// 设置页现在是设置对话框里的一个页签，切换页签会触发 Unloaded，
/// 若在此停掉下载进度轮询，切回本页时进度就不再更新。
/// </summary>
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    /// <summary>窗口关闭时释放页面持有的轮询等资源。</summary>
    public void Release() => (DataContext as System.IDisposable)?.Dispose();
}
