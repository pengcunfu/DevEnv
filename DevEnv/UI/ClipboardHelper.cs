using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace DevEnv.UI;

public static class ClipboardHelper
{
    public static async Task<string?> GetTextAsync(Window? owner = null)
    {
        var clipboard = GetClipboard(owner);
        return clipboard is null ? null : await clipboard.GetTextAsync();
    }

    public static string? GetText(Window? owner = null)
        => GetTextAsync(owner).GetAwaiter().GetResult();

    public static async Task SetTextAsync(string text, Window? owner = null)
    {
        var clipboard = GetClipboard(owner);
        if (clipboard is not null)
            await clipboard.SetTextAsync(text);
    }

    public static void SetText(string text, Window? owner = null)
        => SetTextAsync(text, owner).GetAwaiter().GetResult();

    private static IClipboard? GetClipboard(Window? owner)
    {
        var topLevel = owner is null ? TopLevel.GetTopLevel(GetMainWindow()) : TopLevel.GetTopLevel(owner);
        return topLevel?.Clipboard;
    }

    private static Window? GetMainWindow()
    {
        return Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }
}
