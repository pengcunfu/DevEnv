using System.Windows.Controls;

namespace DevBox.Host.Views;

/// <summary>进程监控页。离开页面时停掉轮询定时器。</summary>
public partial class ProcessesView : UserControl
{
    public ProcessesView()
    {
        InitializeComponent();
        Unloaded += (_, _) => (DataContext as System.IDisposable)?.Dispose();
    }
}