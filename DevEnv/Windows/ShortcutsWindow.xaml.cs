using System.IO;
using System.Windows;
using DevEnv.Host.Infrastructure;
using DevEnv.Host.Views;

namespace DevEnv.Host.Windows;

/// <summary>
/// 快捷方式窗口：本地应用 / 文件夹 / 网站三个横向页签。
/// 非模态、单实例（由 <see cref="ShortcutWindowHost"/> 管理），
/// 支持把 .exe 与文件夹直接拖入窗口添加。
/// </summary>
public partial class ShortcutsWindow : Window
{
    public ShortcutsWindow()
    {
        InitializeComponent();

        AllowDrop = true;
        DragOver += OnDragOver;
        Drop += OnDrop;
    }

    /// <summary>切到指定页签（apps / folders / webs）并刷新该页数据。</summary>
    public void ShowPage(string? pageId)
    {
        Tabs.SelectedIndex = NormalizePageId(pageId);
        ReloadPage(Tabs.SelectedIndex);
    }

    private static int NormalizePageId(string? pageId) => pageId?.Trim().ToLowerInvariant() switch
    {
        "folders" => 1,
        "webs" => 2,
        _ => 0,
    };

    private void ReloadPage(int index)
    {
        switch (index)
        {
            case 1: FoldersTab.Reload(); break;
            case 2: WebsTab.Reload(); break;
            default: AppsTab.Reload(); break;
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>拖入 .exe 加到本地应用、拖入目录加到文件夹，并切到有新增项的页签。</summary>
    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;

        var addedApps = 0;
        var addedFolders = 0;
        foreach (var file in files)
        {
            if (string.IsNullOrWhiteSpace(file))
                continue;

            if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(file))
            {
                if (AppsTab.AddFromDrop(file))
                    addedApps++;
            }
            else if (Directory.Exists(file))
            {
                if (FoldersTab.AddFromDrop(file))
                    addedFolders++;
            }
        }

        if (addedApps + addedFolders == 0)
            return;

        Tabs.SelectedIndex = addedApps > 0 ? 0 : 1;
    }
}
