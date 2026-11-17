using System.Windows.Controls;
using System.Windows.Input;
using DevEnv.Host.ViewModels;

namespace DevEnv.Host.Views;

/// <summary>文本对比。</summary>
public partial class TextDiffView : UserControl
{
    public TextDiffView() => InitializeComponent();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (DataContext is TextDiffViewModel viewModel)
                viewModel.CompareCommand.Execute(null);
            e.Handled = true;
        }
    }
}
