using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace LavaTranslator.Infrastructure;

public static class DialogHelper
{
    public static async Task ShowAsync(Window owner, string message, string title)
    {
        Button? okButton = null;
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Icon = AppIcon.CreateWindowIcon()
        };

        okButton = new Button
        {
            Content = "确定",
            Classes = { "primary" },
            HorizontalAlignment = HorizontalAlignment.Right,
            IsDefault = true
        };
        okButton.Click += (_, _) => dialog.Close();
        DockPanel.SetDock(okButton, Dock.Bottom);
        okButton.Margin = new Thickness(0, 12, 0, 0);

        dialog.Content = new DockPanel
        {
            Margin = new Thickness(16),
            Children =
            {
                okButton,
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };

        await dialog.ShowDialog(owner);
    }

    public static async Task<bool> ConfirmAsync(
        Window owner, string message, string title,
        string okText = "确定", string cancelText = "取消")
    {
        Window? dialog = null;
        var result = false;

        var okButton = new Button
        {
            Content = okText,
            Classes = { "primary" },
            IsDefault = true
        };
        okButton.Click += (_, _) =>
        {
            result = true;
            dialog?.Close();
        };

        var cancelButton = new Button
        {
            Content = cancelText,
            IsCancel = true
        };
        cancelButton.Click += (_, _) => dialog?.Close();

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttons.Children.Add(cancelButton);
        buttons.Children.Add(okButton);

        var buttonsDock = new DockPanel
        {
            Margin = new Thickness(0, 12, 0, 0),
            Children = { buttons }
        };
        DockPanel.SetDock(buttonsDock, Dock.Bottom);

        dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Icon = AppIcon.CreateWindowIcon(),
            Content = new DockPanel
            {
                Margin = new Thickness(16),
                Children =
                {
                    buttonsDock,
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };

        await dialog.ShowDialog(owner);
        return result;
    }
}
