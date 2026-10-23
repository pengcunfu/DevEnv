using System.Windows.Controls;

namespace DevBox.Host.Views;

/// <summary>开发环境页。离开页面时停掉轮询定时器。</summary>
public partial class DevEnvView : UserControl
{
    public DevEnvView()
    {
        InitializeComponent();
        Unloaded += (_, _) => (DataContext as System.IDisposable)?.Dispose();
    }
}
