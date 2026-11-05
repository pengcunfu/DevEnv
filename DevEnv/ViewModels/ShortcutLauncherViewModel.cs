using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using DevEnv.Core;
using DevEnv.Core.Models;
using DevEnv.Core.Services;
using DevEnv.Host.Converters;
using DevEnv.Host.Infrastructure;
using DevEnv.Host.Windows;

namespace DevEnv.Host.ViewModels;

/// <summary>快捷方式种类。</summary>
public enum ShortcutKind
{
    App,
    Folder,
    Web,
}

/// <summary>启动器的一页（本地应用 / 文件夹 / 网站）。</summary>
public sealed record LauncherPage(int Index, string Title, string Glyph);

/// <summary>启动器里的一张图标卡片。</summary>
public sealed class LauncherEntry
{
    public required ShortcutKind Kind { get; init; }
    public required int Index { get; init; }
    public required string Name { get; init; }
    public string IconPath { get; init; } = string.Empty;
    public string TargetPath { get; init; } = string.Empty;

    /// <summary>没有图标时显示的种类字形（Segoe MDL2 Assets）。</summary>
    public string KindGlyph => Kind switch
    {
        ShortcutKind.Folder => "\uE8B7",
        ShortcutKind.Web => "\uE774",
        _ => "\uE8A9",
    };

    public string KindText => Kind switch
    {
        ShortcutKind.Folder => "文件夹",
        ShortcutKind.Web => "网站",
        _ => "应用",
    };

    /// <summary>图标文件存在时才显示图片，否则回退到 KindGlyph。</summary>
    public bool HasIcon => !string.IsNullOrWhiteSpace(IconPath)
        && File.Exists(IconPath);

    public bool Matches(string query) =>
        Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || TargetPath.Contains(query, StringComparison.OrdinalIgnoreCase)
        || KindText.Contains(query, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// 快捷方式启动器（Launchpad 样式）的视图模型：汇总本地应用 / 文件夹 / 网站，
/// 支持搜索、页签切换、添加、改名、刷新图标与移除。
/// </summary>
public sealed partial class ShortcutLauncherViewModel : ObservableObject
{
    private const int AppsPage = 0;
    private const int FoldersPage = 1;
    private const int WebsPage = 2;

    private List<LauncherEntry> _apps = [];
    private List<LauncherEntry> _folders = [];
    private List<LauncherEntry> _webs = [];

    public ShortcutLauncherViewModel() => Reload();

    public IReadOnlyList<LauncherPage> Pages { get; } =
    [
        new(AppsPage, "本地应用", "\uE8A9"),
        new(FoldersPage, "文件夹", "\uE8B7"),
        new(WebsPage, "网站", "\uE774"),
    ];

    public ObservableCollection<LauncherEntry> VisibleEntries { get; } = [];

    /// <summary>当前页签：0 本地应用 / 1 文件夹 / 2 网站。</summary>
    [ObservableProperty]
    private int _selectedPage;

    [ObservableProperty]
    private string _query = string.Empty;

    /// <summary>有搜索词时显示跨页签的合并结果。</summary>
    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _isEmpty = true;

    /// <summary>搜索时的结果条数，显示在网格上方。</summary>
    [ObservableProperty]
    private string _summary = string.Empty;

    /// <summary>操作失败时的提示，显示在底部页签上方。</summary>
    [ObservableProperty]
    private string _message = string.Empty;

    partial void OnSelectedPageChanged(int value)
    {
        // 搜索期间页签被隐藏，切换不生效，避免结果列表跳动
        if (!IsSearching)
            Refresh();
    }

    partial void OnQueryChanged(string value) => Refresh();

    /// <summary>重新读取三类快捷方式；启动器每次打开都调用，保证其他入口的改动可见。</summary>
    public void Reload()
    {
        _apps = CoreServices.Shortcuts.GetApps()
            .Select((item, index) => new LauncherEntry
            {
                Kind = ShortcutKind.App,
                Index = index,
                Name = item.Name,
                IconPath = UpgradeIcon(item.ExecPath, item.IconPath),
                TargetPath = item.ExecPath,
            })
            .ToList();

        _folders = CoreServices.Shortcuts.GetFolders()
            .Select((item, index) => new LauncherEntry
            {
                Kind = ShortcutKind.Folder,
                Index = index,
                Name = item.Name,
                IconPath = item.IconPath,
                TargetPath = item.FolderPath,
            })
            .ToList();

        _webs = CoreServices.Shortcuts.GetWebs()
            .Select((item, index) => new LauncherEntry
            {
                Kind = ShortcutKind.Web,
                Index = index,
                Name = item.Name,
                IconPath = item.IconPath,
                TargetPath = item.Url,
            })
            .ToList();

        Refresh();
    }

    /// <summary>
    /// 旧版本按 32px 缓存应用图标，放大到启动器的 64px 会发虚；
    /// 这里按需重新提取大图标，并让图片转换器丢掉旧缓存。
    /// </summary>
    private static string UpgradeIcon(string execPath, string iconPath)
    {
        var upgraded = IconHelper.EnsureLargeIcon(execPath, iconPath);
        // 原地升级时路径不变，图片转换器的缓存也要丢掉，否则还是旧的小图
        FilePathToImageConverter.Invalidate(iconPath);
        if (!string.Equals(upgraded, iconPath, StringComparison.OrdinalIgnoreCase))
            FilePathToImageConverter.Invalidate(upgraded);
        return upgraded ?? string.Empty;
    }

    private void Refresh()
    {
        var query = Query.Trim();
        IsSearching = query.Length > 0;

        IEnumerable<LauncherEntry> source = IsSearching
            ? _apps.Concat(_folders).Concat(_webs).Where(entry => entry.Matches(query))
            : SelectedPage switch
            {
                FoldersPage => _folders,
                WebsPage => _webs,
                _ => _apps,
            };

        VisibleEntries.Clear();
        foreach (var entry in source)
            VisibleEntries.Add(entry);

        IsEmpty = VisibleEntries.Count == 0;
        Summary = IsSearching ? $"{VisibleEntries.Count} 个匹配项" : string.Empty;
    }

    /// <summary>启动当前页签对应的快捷方式。</summary>
    public void Launch(LauncherEntry entry) => Report(entry.Kind switch
    {
        ShortcutKind.Folder => CoreServices.Shortcuts.OpenFolder(entry.Index),
        ShortcutKind.Web => CoreServices.Shortcuts.OpenWeb(entry.Index),
        _ => CoreServices.Shortcuts.RunApp(entry.Index),
    });

    /// <summary>在资源管理器中定位 / 打开。</summary>
    public void Reveal(LauncherEntry entry)
    {
        if (entry.Kind == ShortcutKind.App)
            Report(CoreServices.Shortcuts.OpenAppFolder(entry.Index));
        else if (entry.Kind == ShortcutKind.Folder)
            HostServices.OpenFolder(entry.TargetPath);
    }

    public void AddApp()
    {
        var path = HostCallbacks.PickExecutable?.Invoke();
        if (string.IsNullOrWhiteSpace(path))
            return;

        Report(CoreServices.Shortcuts.AddApp(path));
        Query = string.Empty;
        SelectedPage = AppsPage;
        Reload();
    }

    public void AddFolder()
    {
        var path = HostCallbacks.PickFolder?.Invoke();
        if (string.IsNullOrWhiteSpace(path))
            return;

        Report(CoreServices.Shortcuts.AddFolder(path));
        Query = string.Empty;
        SelectedPage = FoldersPage;
        Reload();
    }

    public async Task AddWebAsync(Window owner)
    {
        var url = PromptDialog.Show(owner, "输入网址");
        if (string.IsNullOrWhiteSpace(url))
            return;

        var title = PromptDialog.Show(owner, "显示名称（可留空，自动抓取）");
        if (title is null)
            return;

        Message = "正在抓取标题与图标…";
        var result = await CoreServices.Shortcuts.AddWebAsync(
            url, string.IsNullOrWhiteSpace(title) ? null : title);
        Report(result);

        Query = string.Empty;
        SelectedPage = WebsPage;
        Reload();
    }

    /// <summary>底部「＋」按当前页签添加。</summary>
    public async Task AddForCurrentPageAsync(Window owner)
    {
        switch (SelectedPage)
        {
            case FoldersPage:
                AddFolder();
                break;
            case WebsPage:
                await AddWebAsync(owner);
                break;
            default:
                AddApp();
                break;
        }
    }

    /// <summary>拖入的 .exe / 文件夹落到对应页签；返回加入的页签，未识别时返回 null。</summary>
    public int? AddDropped(string path)
    {
        if (File.Exists(path) && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            var result = CoreServices.Shortcuts.AddApp(path);
            Report(result);
            if (!result.Ok)
                return null;
            Query = string.Empty;
            SelectedPage = AppsPage;
            Reload();
            return AppsPage;
        }

        if (Directory.Exists(path))
        {
            var result = CoreServices.Shortcuts.AddFolder(path);
            Report(result);
            if (!result.Ok)
                return null;
            Query = string.Empty;
            SelectedPage = FoldersPage;
            Reload();
            return FoldersPage;
        }

        return null;
    }

    public async Task RenameAsync(LauncherEntry entry, Window owner)
    {
        var name = PromptDialog.Show(owner, "显示名称", entry.Name);
        if (name is null)
            return;

        Report(entry.Kind switch
        {
            ShortcutKind.Folder => CoreServices.Shortcuts.UpdateFolder(entry.Index, name),
            ShortcutKind.Web => await CoreServices.Shortcuts.UpdateWebAsync(entry.Index, name),
            _ => CoreServices.Shortcuts.UpdateApp(entry.Index, name),
        });
        Reload();
    }

    public void Remove(LauncherEntry entry, Window owner)
    {
        if (!ConfirmDialog.Show(owner, $"移除「{entry.Name}」？", "移除"))
            return;
        Report(entry.Kind switch
        {
            ShortcutKind.Folder => CoreServices.Shortcuts.RemoveFolder(entry.Index),
            ShortcutKind.Web => CoreServices.Shortcuts.RemoveWeb(entry.Index),
            _ => CoreServices.Shortcuts.RemoveApp(entry.Index),
        });
        Reload();
    }

    /// <summary>刷新图标：应用重新提取 exe 图标，网站重新抓 favicon；文件夹没有图标源。</summary>
    public async Task RefreshIconAsync(LauncherEntry entry)
    {
        switch (entry.Kind)
        {
            case ShortcutKind.App:
                Report(CoreServices.Shortcuts.UpdateApp(entry.Index, refreshIcon: true));
                FilePathToImageConverter.Invalidate(entry.IconPath);
                break;
            case ShortcutKind.Web:
                Message = "正在刷新图标…";
                Report(await CoreServices.Shortcuts.UpdateWebAsync(entry.Index, refreshIcon: true));
                FilePathToImageConverter.Invalidate(entry.IconPath);
                break;
        }

        Reload();
    }

    private void Report(OperationResult result) => Message = result.Ok ? string.Empty : result.DisplayMessage;
}
