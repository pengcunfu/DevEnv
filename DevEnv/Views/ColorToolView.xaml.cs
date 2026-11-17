using System.Windows.Controls;
using System.Windows.Input;
using DevEnv.Host.ViewModels;

namespace DevEnv.Host.Views;

/// <summary>颜色工具。</summary>
public partial class ColorToolView : UserControl
{
    public ColorToolView() => InitializeComponent();

    private void OnHexKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        if (DataContext is ColorToolViewModel viewModel)
            viewModel.ApplyHexCommand.Execute(null);
        e.Handled = true;
    }
}
