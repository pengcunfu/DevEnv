using System.Windows.Controls;
using DevEnv.Host.ViewModels;

namespace DevEnv.Host.Views;

/// <summary>设置页的常规选项。设置窗口每次打开会复用同一个视图，页签切回时重新读取配置。</summary>
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
                (DataContext as SettingsViewModel)?.Reload();
        };
        Loaded += (_, _) => (DataContext as SettingsViewModel)?.Reload();
    }
}
