using System.Windows.Controls;

namespace DevEnv.Host.Views;

/// <summary>网络测速：离开页面时停掉轮询。</summary>
public partial class SpeedTestView : UserControl
{
    public SpeedTestView()
    {
        InitializeComponent();
        Unloaded += (_, _) => (DataContext as System.IDisposable)?.Dispose();
    }
}
