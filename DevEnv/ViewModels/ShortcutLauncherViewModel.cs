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
    Tool,
    /// <summary>宿主内置工具（JSON / Hosts / 哈希等小工具）。</summary>
    InternalTool,
    /// <summary>启动器文件夹（多个图标的合并磁贴），不是磁盘文件夹快捷方式。</summary>
    Group,
}

/// <summary>启动器的一页（本地应用 / 文件夹 / 网站 / 系统工具 / 内置工具）。</summary>
public sealed record LauncherPage(int Index, string Title, string Glyph);

/// <summary>启动器里的一张图标卡片。</summary>
public sealed partial class LauncherEntry : ObservableObject
{
    public required ShortcutKind Kind { get; init; }
    public required int Index { get; init; }
    public required string Name { get; init; }
    public string IconPath { get; init; } = string.Empty;
    public string TargetPath { get; init; } = string.Empty;

    /// <summary>布局里的稳定标识：普通条目为 app|路径 / folder|路径 / web|url，系统工具为工具键。</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>启动器文件夹的 id；普通条目为空。</summary>
    public string GroupId { get; init; } = string.Empty;

    /// <summary>启动器文件夹内包含的卡片；普通条目为空。</summary>
    public IReadOnlyList<LauncherEntry> GroupItems { get; init; } = [];

    /// <summary>系统工具的分类（如「系统管理」）；其它类型为空。</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>框选 / Ctrl+单击的选中态。</summary>
    [ObservableProperty]
    private bool _isSelected;

    public bool IsGroup => Kind == ShortcutKind.Group;

    public bool IsTool => Kind == ShortcutKind.Tool;

    /// <summary>文件夹磁贴的 2×2 预览，最多取前四项。</summary>
    public IReadOnlyList<LauncherEntry> GroupPreview =>
        GroupItems.Count <= 4 ? GroupItems : GroupItems.Take(4).ToList();

    /// <summary>没有图标时显示的种类字形（Segoe MDL2 Assets）。</summary>
    public string KindGlyph => Kind switch
    {
        ShortcutKind.Folder => "",
        ShortcutKind.Web => "",
        ShortcutKind.Tool => "",
        ShortcutKind.InternalTool => "",
        ShortcutKind.Group => "",
        _ => "",
    };

    public string KindText => Kind switch
    {
        ShortcutKind.Folder => "文件夹",
        ShortcutKind.Web => "网站",
        ShortcutKind.Tool => string.IsNullOrEmpty(Category) ? "系统工具" : Category,
        ShortcutKind.InternalTool => "内置工具",
        ShortcutKind.Group => $"{GroupItems.Count} 项",
        _ => "应用",
    };

    /// <summary>系统工具与内置工具卡片始终显示分类；快捷方式只在搜索时显示类型。</summary>
    public bool IsToolCard => Kind is ShortcutKind.Tool or ShortcutKind.InternalTool;

    /// <summary>图标文件存在时才显示图片，否则回退到 KindGlyph；文件夹磁贴用 2×2 预览。</summary>
    public bool HasIcon => !IsGroup && !string.IsNullOrWhiteSpace(IconPath) && File.Exists(IconPath);

    public bool Matches(string query) =>
        Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || TargetPath.Contains(query, StringComparison.OrdinalIgnoreCase)
        || KindText.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Category.Contains(query, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// 全局启动器（Launchpad 样式）的视图模型：汇总本地应用 / 文件夹 / 网站 / 系统工具 / 内置工具，
/// 支持搜索、页签切换、添加、改名、刷新图标、移除、自定义排序、多选与启动器文件夹。
/// </summary>
public sealed partial class ShortcutLauncherViewModel : ObservableObject
{
    private const int AppsPage = 0;
    private const int FoldersPage = 1;
    private const int WebsPage = 2;
    private const int ToolsPage = 3;
    private const int BuiltinToolsPage = 4;

    /// <summary>系统工具的分类排序；目录里新增分类时排在已知分类之后。</summary>
    private static readonly string[] ToolCategoryOrder =
        ["系统管理", "硬件与设备", "网络与安全", "性能与监控"];

    /// <summary>内置工具页里代表宿主主窗口的条目 key。</summary>
    private const string MainWindowToolKey = "main-window";

    /// <summary>宿主内置工具：tool id、显示名与说明，顺序与主窗口原来的「工具」菜单一致。</summary>
    private static readonly (string Key, string Name, string Description)[] BuiltinToolDefinitions =
    [
        (MainWindowToolKey, "燧岚开发盒子", "开发环境主窗口"),
        ("json", "JSON 格式化器", "格式化与校验 JSON"),
        ("hosts", "Hosts 文件编辑器", "编辑系统 hosts 解析记录"),
        ("image", "图像格式转换器", "图像格式转换与压缩"),
        ("hash", "文件哈希计算器", "计算文件校验值"),
        ("file-tools", "文件解锁与粉碎", "解除占用与安全粉碎"),
        ("startup", "启动项管理", "查看与管理开机启动项"),
        ("context-menu", "右键菜单管理", "自定义资源管理器右键菜单"),
    ];

    private readonly Dictionary<string, LauncherEntry> _itemsByKey =
        new(StringComparer.OrdinalIgnoreCase);

    private List<LauncherEntry> _apps = [];
    private List<LauncherEntry> _folders = [];
    private List<LauncherEntry> _webs = [];
    private List<LauncherEntry> _tools = [];
    private List<LauncherEntry> _builtinTools = [];
    private List<LauncherEntry> _allItems = [];
    private List<LauncherEntry>[] _pageTiles = [[], [], []];
    private LauncherLayout _layout = new();

    /// <summary>批量重建时暂时屏蔽属性变更回调里的 Refresh，避免重复刷新。</summary>
    private bool _suspendRefresh;

    public ShortcutLauncherViewModel() => Reload();

    public IReadOnlyList<LauncherPage> Pages { get; } =
    [
        new(AppsPage, "本地应用", ""),
        new(FoldersPage, "文件夹", ""),
        new(WebsPage, "网站", ""),
        new(ToolsPage, "系统工具", ""),
        new(BuiltinToolsPage, "内置工具", ""),
    ];

    public ObservableCollection<LauncherEntry> VisibleEntries { get; } = [];

    /// <summary>当前页签：0 本地应用 / 1 文件夹 / 2 网站 / 3 系统工具 / 4 内置工具。</summary>
    [ObservableProperty]
    private int _selectedPage;

    [ObservableProperty]
    private string _query = string.Empty;

    /// <summary>有搜索词时显示跨页签的合并结果。</summary>
    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _isEmpty = true;

    /// <summary>搜索时的结果条数或文件夹内的条目数，显示在网格上方。</summary>
    [ObservableProperty]
    private string _summary = string.Empty;

    /// <summary>操作失败时的提示，显示在底部页签上方。</summary>
    [ObservableProperty]
    private string _message = string.Empty;

    /// <summary>当前展开的启动器文件夹；为空表示在普通页签。</summary>
    [ObservableProperty]
    private LauncherEntry? _currentGroup;

    public bool IsInsideGroup => CurrentGroup is not null;

    /// <summary>系统工具页与内置工具页是固定目录，只读：不能框选、拖动、分组或添加。</summary>
    private static bool IsReadOnlyPage(int page) => page is ToolsPage or BuiltinToolsPage;

    /// <summary>当前视图是否允许框选、拖动、排序与分组。</summary>
    public bool IsEditableView => !IsSearching && (IsInsideGroup || !IsReadOnlyPage(SelectedPage));

    /// <summary>只有普通页签能创建新文件夹，固定工具页与文件夹内不行。</summary>
    public bool CanCreateGroup => !IsSearching && !IsInsideGroup && !IsReadOnlyPage(SelectedPage);

    /// <summary>固定工具页与文件夹内不需要「添加」，右下角按钮随之隐藏。</summary>
    public bool CanAddOnCurrentPage => !IsInsideGroup && !IsReadOnlyPage(SelectedPage);

    public IReadOnlyList<LauncherEntry> SelectedEntries =>
        VisibleEntries.Where(entry => entry.IsSelected).ToList();

    public bool HasSelection => VisibleEntries.Any(entry => entry.IsSelected);

    partial void OnSelectedPageChanged(int value)
    {
        ClearSelection();
        if (IsInsideGroup)
        {
            _suspendRefresh = true;
            CurrentGroup = null;
            _suspendRefresh = false;
        }

        NotifyEditableChanged();
        if (!IsSearching)
            Refresh();
    }

    partial void OnQueryChanged(string value)
    {
        ClearSelection();
        if (IsInsideGroup && !string.IsNullOrWhiteSpace(value))
        {
            _suspendRefresh = true;
            CurrentGroup = null;
            _suspendRefresh = false;
        }

        NotifyEditableChanged();
        Refresh();
    }

    partial void OnCurrentGroupChanged(LauncherEntry? value)
    {
        OnPropertyChanged(nameof(IsInsideGroup));
        NotifyEditableChanged();
        ClearSelection();
        if (!_suspendRefresh)
            Refresh();
    }

    private void NotifyEditableChanged()
    {
        OnPropertyChanged(nameof(IsEditableView));
        OnPropertyChanged(nameof(CanCreateGroup));
        OnPropertyChanged(nameof(CanAddOnCurrentPage));
    }

    /// <summary>重新读取本地应用 / 文件夹 / 网站 / 系统工具与内置工具；启动器每次打开都调用，保证其他入口的改动可见。</summary>
    public void Reload()
    {
        _suspendRefresh = true;
        CurrentGroup = null;
        _itemsByKey.Clear();

        _apps = CoreServices.Shortcuts.GetApps()
            .Select((item, index) => new LauncherEntry
            {
                Kind = ShortcutKind.App,
                Index = index,
                Key = LauncherLayout.AppKey(item.ExecPath),
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
                Key = LauncherLayout.FolderKey(item.FolderPath),
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
                Key = LauncherLayout.WebKey(item.Url),
                Name = item.Name,
                IconPath = item.IconPath,
                TargetPath = item.Url,
            })
            .ToList();

        // 分类内保持目录原顺序；OrderBy 是稳定排序
        _tools = CoreServices.Shortcuts.GetSystemTools()
            .OrderBy(item =>
            {
                var rank = Array.IndexOf(ToolCategoryOrder, item.Category);
                return rank < 0 ? int.MaxValue : rank;
            })
            .Select((item, index) => new LauncherEntry
            {
                Kind = ShortcutKind.Tool,
                Index = index,
                Key = item.Key,
                Name = item.Name,
                Category = item.Category,
                IconPath = item.IconPath,
                TargetPath = item.Description,
            })
            .ToList();

        _builtinTools = BuiltinToolDefinitions
            .Select((item, index) => new LauncherEntry
            {
                Kind = ShortcutKind.InternalTool,
                Index = index,
                Key = item.Key,
                Name = item.Name,
                TargetPath = item.Description,
            })
            .ToList();

        _allItems = _apps.Concat(_folders).Concat(_webs).Concat(_tools).Concat(_builtinTools).ToList();
        foreach (var entry in _allItems)
            _itemsByKey[entry.Key] = entry;

        _layout = CoreServices.Shortcuts.ReadLauncherLayout();
        _layout.Normalize(
        [
            _apps.Select(entry => entry.Key).ToList(),
            _folders.Select(entry => entry.Key).ToList(),
            _webs.Select(entry => entry.Key).ToList(),
        ]);
        RebuildTiles();

        _suspendRefresh = false;
        NotifyEditableChanged();
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

    /// <summary>按布局重建每个页签的顶层卡片（普通条目 + 文件夹磁贴）。</summary>
    private void RebuildTiles()
    {
        _pageTiles = new List<LauncherEntry>[LauncherLayout.EditablePageCount];
        for (var page = 0; page < LauncherLayout.EditablePageCount; page++)
        {
            _pageTiles[page] = _layout.Page(page).Tiles
                .Select(BuildTile)
                .Where(entry => entry is not null)
                .Cast<LauncherEntry>()
                .ToList();
        }
    }

    private LauncherEntry? BuildTile(LauncherLayoutTile tile)
    {
        if (!tile.IsGroup)
            return _itemsByKey.GetValueOrDefault(tile.Key);

        var children = tile.Items
            .Select(key => _itemsByKey.GetValueOrDefault(key))
            .Where(entry => entry is not null)
            .Cast<LauncherEntry>()
            .ToList();
        if (children.Count < 2)
            return null;

        return new LauncherEntry
        {
            Kind = ShortcutKind.Group,
            Index = -1,
            Key = $"group|{tile.Id}",
            GroupId = tile.Id,
            Name = tile.Name,
            GroupItems = children,
        };
    }

    private void Refresh()
    {
        if (_suspendRefresh)
            return;

        var query = Query.Trim();
        IsSearching = query.Length > 0;

        IEnumerable<LauncherEntry> source;
        if (IsSearching)
            source = _allItems.Where(entry => entry.Matches(query));
        else if (CurrentGroup is not null)
            source = CurrentGroup.GroupItems;
        else if (SelectedPage == ToolsPage)
            source = _tools;
        else if (SelectedPage == BuiltinToolsPage)
            source = _builtinTools;
        else
            source = _pageTiles[SelectedPage];

        VisibleEntries.Clear();
        foreach (var entry in source)
            VisibleEntries.Add(entry);

        IsEmpty = !IsInsideGroup && VisibleEntries.Count == 0;
        Summary = IsSearching
            ? $"{VisibleEntries.Count} 个匹配项"
            : IsInsideGroup
                ? $"{VisibleEntries.Count} 项"
                : string.Empty;

        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedEntries));
    }

    #region 启动器文件夹

    /// <summary>打开启动器文件夹；普通卡片忽略。</summary>
    public void OpenGroup(LauncherEntry entry)
    {
        if (!entry.IsGroup)
            return;

        Message = string.Empty;
        Query = string.Empty;
        CurrentGroup = entry;
    }

    public void CloseGroup()
    {
        Query = string.Empty;
        CurrentGroup = null;
        Refresh();
    }

    /// <summary>把若干卡片拖到目标卡片上：目标不是文件夹时新建文件夹，目标是文件夹时加入其中。</summary>
    public void MergeIntoFolder(IReadOnlyList<LauncherEntry> sources, LauncherEntry target, string name = "文件夹")
    {
        if (IsSearching || IsInsideGroup || IsReadOnlyPage(SelectedPage))
            return;

        if (target.IsGroup)
        {
            AddToGroup(sources, target);
            return;
        }

        var all = sources
            .Append(target)
            .Where(entry => !entry.IsGroup
                && entry.Kind is not (ShortcutKind.Tool or ShortcutKind.InternalTool))
            .GroupBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        if (all.Count < 2)
            return;

        var insertIndex = VisibleEntries.IndexOf(target);
        if (insertIndex < 0)
            insertIndex = VisibleEntries.Count;

        if (_layout.CreateGroup(SelectedPage, name, all.Select(entry => entry.Key).ToList(), insertIndex) is not null)
            Commit();
    }

    public void AddToGroup(IReadOnlyList<LauncherEntry> sources, LauncherEntry group)
    {
        if (!group.IsGroup)
            return;

        var keys = sources
            .Where(entry => !entry.IsGroup
                && entry.Kind is not (ShortcutKind.Tool or ShortcutKind.InternalTool))
            .Select(entry => entry.Key)
            .Where(key => !group.GroupItems.Any(child =>
                string.Equals(child.Key, key, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (keys.Count == 0)
            return;

        _layout.AddToGroup(group.GroupId, keys);
        Commit();
    }

    public void MoveOutOfGroup(IReadOnlyList<LauncherEntry> entries)
    {
        if (CurrentGroup is null)
            return;

        var keys = entries
            .Where(entry => !entry.IsGroup
                && entry.Kind is not (ShortcutKind.Tool or ShortcutKind.InternalTool))
            .Select(entry => entry.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (keys.Count == 0)
            return;

        var groupId = CurrentGroup.GroupId;
        _layout.MoveOutOfGroup(groupId, keys);
        if (_layout.FindGroup(groupId) is null)
            CloseGroupWithoutRefresh();
        Commit();
    }

    public void RenameGroup(LauncherEntry group, string name)
    {
        if (!group.IsGroup || string.IsNullOrWhiteSpace(name))
            return;

        _layout.RenameGroup(group.GroupId, name);
        Commit();
    }

    public void DissolveGroup(LauncherEntry group)
    {
        if (!group.IsGroup)
            return;

        var groupId = group.GroupId;
        _layout.DissolveGroup(groupId);
        if (string.Equals(CurrentGroup?.GroupId, groupId, StringComparison.OrdinalIgnoreCase))
            CloseGroupWithoutRefresh();
        Commit();
    }

    private void CloseGroupWithoutRefresh()
    {
        _suspendRefresh = true;
        CurrentGroup = null;
        _suspendRefresh = false;
    }

    #endregion

    #region 排序与选择

    /// <summary>把卡片移动到页签内的指定位置；文件夹内则移动条目。</summary>
    public void MoveTiles(IReadOnlyList<LauncherEntry> entries, int insertIndex)
    {
        if (IsInsideGroup)
        {
            MoveGroupItems(entries, insertIndex);
            return;
        }

        if (IsReadOnlyPage(SelectedPage))
            return;

        var ids = entries
            .Select(TileId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (ids.Count == 0)
            return;

        _layout.MoveTiles(SelectedPage, ids, insertIndex);
        Commit();
    }

    private void MoveGroupItems(IReadOnlyList<LauncherEntry> entries, int insertIndex)
    {
        if (CurrentGroup is null)
            return;

        var keys = entries
            .Where(entry => !entry.IsGroup)
            .Select(entry => entry.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (keys.Count == 0)
            return;

        _layout.MoveGroupItems(CurrentGroup.GroupId, keys, insertIndex);
        Commit();
    }

    private static string TileId(LauncherEntry entry) =>
        entry.IsGroup ? $"group|{entry.GroupId}" : entry.Key;

    public void ClearSelection()
    {
        var changed = false;
        foreach (var entry in VisibleEntries)
        {
            if (!entry.IsSelected)
                continue;
            entry.IsSelected = false;
            changed = true;
        }

        if (changed)
            NotifySelectionChanged();
    }

    public void SetSelection(IEnumerable<LauncherEntry> entries)
    {
        var wanted = new HashSet<LauncherEntry>(entries);
        foreach (var entry in VisibleEntries)
        {
            var shouldSelect = wanted.Contains(entry);
            if (entry.IsSelected != shouldSelect)
                entry.IsSelected = shouldSelect;
        }

        NotifySelectionChanged();
    }

    public void ToggleSelection(LauncherEntry entry)
    {
        entry.IsSelected = !entry.IsSelected;
        NotifySelectionChanged();
    }

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedEntries));
    }

    private void Commit()
    {
        CoreServices.Shortcuts.SaveLauncherLayout(_layout);

        var groupId = CurrentGroup?.GroupId;
        RebuildTiles();
        if (groupId is not null)
        {
            _suspendRefresh = true;
            CurrentGroup = FindGroup(groupId);
            _suspendRefresh = false;
        }

        ClearSelection();
        Refresh();
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedEntries));
    }

    /// <summary>
    /// 自检探针专用：在当前页临时插入一个文件夹磁贴，用来验证 2×2 预览与文件夹内视图能正常加载。
    /// 只改内存，不写 data.json。
    /// </summary>
    internal bool AddDiagnosticGroup()
    {
        if (!IsEditableView)
            return false;

        var children = _pageTiles[SelectedPage]
            .Where(entry => !entry.IsGroup)
            .Take(2)
            .ToList();
        if (children.Count < 2)
            return false;

        _pageTiles[SelectedPage].Insert(0, new LauncherEntry
        {
            Kind = ShortcutKind.Group,
            Index = -1,
            Key = "group|probe",
            GroupId = "probe",
            Name = "自检文件夹",
            GroupItems = children,
        });
        Refresh();
        return true;
    }

    /// <summary>自检探针专用：移除 <see cref="AddDiagnosticGroup"/> 插入的临时磁贴。</summary>
    internal void RemoveDiagnosticGroup()
    {
        RebuildTiles();
        Refresh();
    }

    private LauncherEntry? FindGroup(string groupId) =>
        _pageTiles.SelectMany(tiles => tiles)
            .FirstOrDefault(entry => entry.IsGroup
                && string.Equals(entry.GroupId, groupId, StringComparison.OrdinalIgnoreCase));

    #endregion

    /// <summary>启动当前页签对应的快捷方式。</summary>
    public void Launch(LauncherEntry entry)
    {
        if (entry.IsGroup)
        {
            OpenGroup(entry);
            return;
        }

        Report(entry.Kind switch
        {
            ShortcutKind.Folder => CoreServices.Shortcuts.OpenFolder(entry.Index),
            ShortcutKind.Web => CoreServices.Shortcuts.OpenWeb(entry.Index),
            ShortcutKind.Tool => CoreServices.Shortcuts.RunSystemTool(entry.Key),
            ShortcutKind.InternalTool => entry.Key == MainWindowToolKey
                ? ShowMainWindow()
                : ToolWindowHost.Open(entry.Key),
            _ => CoreServices.Shortcuts.RunApp(entry.Index),
        });
    }

    /// <summary>在资源管理器中定位 / 打开。</summary>
    public void Reveal(LauncherEntry entry)
    {
        if (entry.Kind == ShortcutKind.App)
            Report(CoreServices.Shortcuts.OpenAppFolder(entry.Index));
        else if (entry.Kind == ShortcutKind.Folder)
            HostServices.OpenFolder(entry.TargetPath);
        // 系统工具、内置工具与启动器文件夹没有可定位的本地路径
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
            case ToolsPage:
            case BuiltinToolsPage:
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
        var name = PromptDialog.Show(owner, entry.IsGroup ? "文件夹名称" : "显示名称", entry.Name);
        if (name is null)
            return;

        if (entry.IsGroup)
        {
            RenameGroup(entry, name);
            return;
        }

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
        if (entry.IsGroup)
        {
            DissolveGroup(entry);
            return;
        }

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

    /// <summary>批量移除当前选中的卡片；选中文件夹时只解散，不删除其中的快捷方式。</summary>
    public void RemoveSelected(Window owner)
    {
        var selected = SelectedEntries;
        if (selected.Count == 0)
            return;

        if (!ConfirmDialog.Show(owner, $"移除选中的 {selected.Count} 项？", "移除"))
            return;

        foreach (var group in selected.Where(entry => entry.IsGroup))
            _layout.DissolveGroup(group.GroupId);

        var items = selected.Where(entry => !entry.IsGroup).ToList();
        foreach (var batch in items.GroupBy(entry => entry.Kind))
        {
            foreach (var entry in batch.OrderByDescending(entry => entry.Index))
            {
                switch (entry.Kind)
                {
                    case ShortcutKind.Folder:
                        CoreServices.Shortcuts.RemoveFolder(entry.Index);
                        break;
                    case ShortcutKind.Web:
                        CoreServices.Shortcuts.RemoveWeb(entry.Index);
                        break;
                    default:
                        CoreServices.Shortcuts.RemoveApp(entry.Index);
                        break;
                }
            }
        }

        CoreServices.Shortcuts.SaveLauncherLayout(_layout);
        Reload();
    }

    /// <summary>刷新图标：应用重新提取 exe 图标，网站重新抓 favicon；文件夹没有图标源。</summary>
    public async Task RefreshIconAsync(LauncherEntry entry)
    {
        if (entry.IsGroup)
            return;

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

    /// <summary>显示宿主主窗口（开发环境），与托盘「显示主窗口」走同一条回调。</summary>
    private static OperationResult ShowMainWindow()
    {
        var show = HostCallbacks.OnShowMainWindow;
        if (show is null)
            return OperationResult.Fail("主窗口暂不可用");

        show();
        return OperationResult.Success();
    }
}
