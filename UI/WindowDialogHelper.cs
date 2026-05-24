using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace DevEnv.UI;

public static class WindowDialogHelper
{
    public static Window? GetMainWindow()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }

    public static void ShowOwned(this Window window, Window? owner)
    {
        if (owner is null)
            window.Show();
        else
            window.Show(owner);
    }

    public static void ShowDialogBlocking(this Window dialog, Window? owner = null)
    {
        owner ??= GetMainWindow();
        if (owner is null)
            dialog.Show();
        else
            dialog.ShowDialog(owner).GetAwaiter().GetResult();
    }

    public static bool ShowDialogBool(this Window dialog, Window? owner = null)
    {
        owner ??= GetMainWindow();
        return dialog.ShowDialog<bool>(owner ?? throw new InvalidOperationException("需要父窗口才能显示对话框。"))
            .GetAwaiter().GetResult();
    }
}
