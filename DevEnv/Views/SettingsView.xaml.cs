using System.Windows.Controls;

namespace DevEnv.Host.Views;

/// <summary>设置页。离开页面时停掉下载进度轮询。</summary>
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        Unloaded += (_, _) => (DataContext as System.IDisposable)?.Dispose();
    }
}