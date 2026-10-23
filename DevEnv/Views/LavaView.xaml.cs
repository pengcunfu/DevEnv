using System.Windows.Controls;
using DevEnv.Host.ViewModels;

namespace DevEnv.Host.Views;

/// <summary>熔岩环境页。离开页面时停掉轮询定时器，避免视图模型被 DispatcherTimer 挂住不放。</summary>
public partial class LavaView : UserControl
{
    public LavaView()
    {
        InitializeComponent();
        Tabs.SelectionChanged += (_, _) => OnTabChanged(Tabs.SelectedIndex);
        Unloaded += (_, _) => (DataContext as System.IDisposable)?.Dispose();
    }

    private LavaViewModel ViewModel => (LavaViewModel)DataContext;

    private void OnTabChanged(int index) => ViewModel.OnTabChanged(index);
}
