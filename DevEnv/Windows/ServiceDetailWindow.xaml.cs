using System.Windows;
using DevEnv.Host.ViewModels;

namespace DevEnv.Host.Windows;

/// <summary>
/// 服务详情模态框：展示描述、启动类型、路径与依赖；可改启动类型或打开目录。
/// 与列表共用 <see cref="ServicesViewModel"/>，应用后列表仍会刷新。
/// </summary>
public partial class ServiceDetailWindow : Window
{
    private ServiceDetailWindow(ServicesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Title = viewModel.Selected is { } selected
            ? $"服务详情 - {selected.DisplayName}"
            : "服务详情";
    }

    /// <summary>打开详情；无选中项时直接返回。</summary>
    public static void ShowDialog(Window? owner, ServicesViewModel viewModel)
    {
        if (viewModel.Selected is null)
            return;

        var window = new ServiceDetailWindow(viewModel);
        if (owner is not null && owner.IsVisible)
            window.Owner = owner;
        window.ShowDialog();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
