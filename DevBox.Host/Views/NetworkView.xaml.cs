using System.Windows.Controls;
using DevBox.Host.ViewModels;

namespace DevBox.Host.Views;

/// <summary>网络监控页。页签切换决定哪个定时器在刷新；离开页面时全部停掉。</summary>
public partial class NetworkView : UserControl
{
    public NetworkView()
    {
        InitializeComponent();
        Tabs.SelectionChanged += (_, _) => ViewModel.OnTabChanged(Tabs.SelectedIndex);
        Unloaded += (_, _) => (DataContext as System.IDisposable)?.Dispose();
    }

    private NetworkViewModel ViewModel => (NetworkViewModel)DataContext;
}