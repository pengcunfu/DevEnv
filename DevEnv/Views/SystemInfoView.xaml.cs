using System.Windows;
using System.Windows.Controls;
using DevEnv.Host.ViewModels;

namespace DevEnv.Host.Views;

/// <summary>系统信息：首次显示时在后台读取一次。</summary>
public partial class SystemInfoView : UserControl
{
    public SystemInfoView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (DataContext is SystemInfoViewModel viewModel)
            await viewModel.LoadOnceAsync();
    }
}
