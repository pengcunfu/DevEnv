using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace DevEnv.UI;

public enum MessageBoxResult
{
    None,
    OK,
    Cancel,
    Yes,
    No
}

public enum MessageBoxButton
{
    OK,
    OKCancel,
    YesNo,
    YesNoCancel
}

public enum MessageBoxImage
{
    None,
    Error,
    Warning,
    Information,
    Question
}

public static class MessageBox
{
    public static MessageBoxResult Show(
        string messageBoxText,
        string caption,
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None)
    {
        if (Dispatcher.UIThread.CheckAccess())
            return ShowInternal(messageBoxText, caption, button).GetAwaiter().GetResult();

        return Dispatcher.UIThread.InvokeAsync(() =>
            ShowInternal(messageBoxText, caption, button)).GetAwaiter().GetResult();
    }

    private static Window? GetOwner()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }

    private static async Task<MessageBoxResult> ShowInternal(
        string message,
        string caption,
        MessageBoxButton button)
    {
        var owner = GetOwner();

        var dialog = new Window
        {
            Title = caption,
            Width = 420,
            MinHeight = 140,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = owner is null
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false
        };

        var messageBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
            Foreground = Brushes.Black
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        void AddButton(string text, MessageBoxResult result, bool isDefault = false)
        {
            var btn = new Button
            {
                Content = text,
                MinWidth = 72,
                IsDefault = isDefault
            };
            btn.Click += (_, _) => dialog.Close(result);
            buttonPanel.Children.Add(btn);
        }

        switch (button)
        {
            case MessageBoxButton.OKCancel:
                AddButton("取消", MessageBoxResult.Cancel);
                AddButton("确定", MessageBoxResult.OK, true);
                break;
            case MessageBoxButton.YesNo:
                AddButton("否", MessageBoxResult.No);
                AddButton("是", MessageBoxResult.Yes, true);
                break;
            case MessageBoxButton.YesNoCancel:
                AddButton("取消", MessageBoxResult.Cancel);
                AddButton("否", MessageBoxResult.No);
                AddButton("是", MessageBoxResult.Yes, true);
                break;
            default:
                AddButton("确定", MessageBoxResult.OK, true);
                break;
        }

        dialog.Content = new Border
        {
            Padding = new Thickness(20),
            Child = new StackPanel
            {
                Children = { messageBlock, buttonPanel }
            }
        };

        if (owner is null)
        {
            var tcs = new TaskCompletionSource<MessageBoxResult>();
            dialog.Closed += (_, _) => tcs.TrySetResult(MessageBoxResult.None);
            dialog.Show();
            return await tcs.Task;
        }

        return await dialog.ShowDialog<MessageBoxResult>(owner);
    }
}


