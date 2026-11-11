using System.Windows.Controls;

namespace DevEnv.Host.Views;

/// <summary>端口管理：离开页面时停掉定时器。</summary>
public partial class PortsView : UserControl
{
    public PortsView()
    {
        InitializeComponent();
        Unloaded += (_, _) => (DataContext as System.IDisposable)?.Dispose();
    }
}
