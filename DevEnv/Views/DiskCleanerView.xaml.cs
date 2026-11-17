using System.Windows.Controls;
using DevEnv.Host.ViewModels;

namespace DevEnv.Host.Views;

/// <summary>C 盘清理：首次显示自动扫描一次，关闭时取消仍在进行的扫描 / 清理。</summary>
public partial class DiskCleanerView : UserControl
{
    public DiskCleanerView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        (DataContext as DiskCleanerViewModel)?.AutoScan();
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        (DataContext as DiskCleanerViewModel)?.Dispose();
    }
}
