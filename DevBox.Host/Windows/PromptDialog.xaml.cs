using System.Windows;

namespace DevBox.Host.Windows;

/// <summary>单行输入对话框，替代 React 时代的 window.prompt。取消返回 null。</summary>
public partial class PromptDialog : Window
{
    private PromptDialog(string message, string initial)
    {
        InitializeComponent();
        MessageText.Text = message;
        MessageText.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
        InputBox.Text = initial ?? string.Empty;
        Loaded += (_, _) => { InputBox.Focus(); InputBox.SelectAll(); };
    }

    public static string? Show(Window? owner, string message, string initial = "")
    {
        var dialog = new PromptDialog(message, initial);
        if (owner is not null && owner.IsVisible)
            dialog.Owner = owner;
        return dialog.ShowDialog() == true ? dialog.InputBox.Text : null;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}