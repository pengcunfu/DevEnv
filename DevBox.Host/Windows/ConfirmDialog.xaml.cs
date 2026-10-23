using System.Windows;

namespace DevBox.Host.Windows;

/// <summary>确认对话框，替代 React 时代的 window.confirm。</summary>
public partial class ConfirmDialog : Window
{
    private ConfirmDialog(string message, string? confirmText)
    {
        InitializeComponent();
        MessageText.Text = message;
        if (!string.IsNullOrWhiteSpace(confirmText))
            OkButton.Content = confirmText;
    }

    public static bool Show(Window? owner, string message, string? confirmText = null)
    {
        var dialog = new ConfirmDialog(message, confirmText);
        if (owner is not null && owner.IsVisible)
            dialog.Owner = owner;
        return dialog.ShowDialog() == true;
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