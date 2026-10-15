using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ProcessWatch.UI;

internal static class PwDialog
{
    public static async Task ShowAsync(Window? owner, string message, string title)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            MinHeight = 160,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };

        var ok = new Button
        {
            Content = "确定",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
            MinWidth = 80,
        };
        ok.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                },
                ok,
            },
        };

        if (owner is null)
            dialog.Show();
        else
            await dialog.ShowDialog(owner);
    }

    public static async Task<bool> ConfirmAsync(Window? owner, string message, string title)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            MinHeight = 160,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };

        var result = false;
        var yes = new Button
        {
            Content = "确定",
            MinWidth = 80,
            Margin = new Thickness(0, 0, 8, 0),
        };
        yes.Click += (_, _) =>
        {
            result = true;
            dialog.Close();
        };

        var no = new Button
        {
            Content = "取消",
            MinWidth = 80,
        };
        no.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 16, 0, 0),
                    Children = { yes, no },
                },
            },
        };

        if (owner is null)
            dialog.Show();
        else
            await dialog.ShowDialog(owner);

        return result;
    }
}
