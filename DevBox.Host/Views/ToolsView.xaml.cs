using System.Windows;
using System.Windows.Controls;
using DevBox.Core.Models;
using DevBox.Host.ViewModels;

namespace DevBox.Host.Views;

/// <summary>系统工具页。点磁贴启动对应的 Windows 工具（或跳到内置工具页）。</summary>
public partial class ToolsView : UserControl
{
    public ToolsView()
    {
        InitializeComponent();
        DataContext = new ToolsViewModel();
    }

    private ToolsViewModel ViewModel => (ToolsViewModel)DataContext;

    private void OnToolClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ToolInfo tool })
            ViewModel.Run(tool);
    }
}