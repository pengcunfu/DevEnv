using System.Windows.Controls;

namespace DevEnv.Host.Views;

/// <summary>服务监控页。离开页面时停掉轮询定时器。</summary>
public partial class ServicesView : UserControl
{
    public ServicesView()
    {
        InitializeComponent();
        Unloaded += (_, _) => (DataContext as System.IDisposable)?.Dispose();
    }
}
