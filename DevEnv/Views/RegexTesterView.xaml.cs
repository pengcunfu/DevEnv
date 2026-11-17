using System.Windows.Controls;
using System.Windows.Input;
using DevEnv.Host.ViewModels;

namespace DevEnv.Host.Views;

/// <summary>正则测试器。</summary>
public partial class RegexTesterView : UserControl
{
    public RegexTesterView() => InitializeComponent();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (DataContext is RegexTesterViewModel viewModel)
                viewModel.TestCommand.Execute(null);
            e.Handled = true;
        }
    }
}
