using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace DevEnv.UI;

public static class FileDialogs
{
    public static async Task<string[]?> OpenFilesAsync(
        Window? owner,
        string title,
        bool allowMultiple = false,
        string? filterName = null,
        IReadOnlyList<string>? extensions = null)
    {
        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel?.StorageProvider is not { } storage)
            return null;

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = allowMultiple,
            FileTypeFilter = extensions is { Count: > 0 }
                ? new[]
                {
                    new FilePickerFileType(filterName ?? "Files")
                    {
                        Patterns = extensions.Select(e => e.StartsWith('*') ? e : $"*.{e}").ToList()
                    }
                }
                : null
        };

        var files = await storage.OpenFilePickerAsync(options);
        return files.Count == 0
            ? null
            : files.Select(f => f.Path.LocalPath).ToArray();
    }

    public static async Task<string?> OpenFolderAsync(Window? owner, string title)
    {
        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel?.StorageProvider is not { } storage)
            return null;

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.Count == 0 ? null : folders[0].Path.LocalPath;
    }
}
