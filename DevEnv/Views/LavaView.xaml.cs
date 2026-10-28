using System.Windows.Controls;
using DevEnv.Host.ViewModels;

namespace DevEnv.Host.Views;

/// <summary>熔岩环境页。离开页面时停掉轮询定时器，避免视图模型被 DispatcherTimer 挂住不放。</summary>
public partial class LavaView : UserControl
{
    public LavaView()
    {
        InitializeComponent();
        Tabs.SelectionChanged += OnTabsSelectionChanged;
        Unloaded += (_, _) => (DataContext as System.IDisposable)?.Dispose();
    }

    private LavaViewModel ViewModel => (LavaViewModel)DataContext;

    /// <summary>只响应 TabControl 自己的选择变化，忽略内部 DataGrid 冒泡上来的事件。</summary>
    private void OnTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, Tabs))
            return;

        ViewModel.OnTabChanged(Tabs.SelectedIndex);
    }
}
