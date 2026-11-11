using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
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
    /// <summary>Windows 开始菜单里的快捷方式；只读，可启动或收进启动器。</summary>
    StartMenu,
    /// <summary>启动器文件夹（多个图标的合并磁贴），不是磁盘文件夹快捷方式。</summary>
    Group,
}

/// <summary>启动器的一页（本地应用 / 文件夹 / 网站）。</summary>
public sealed record LauncherPage(int Index, string Title, string Glyph);

/// <summary>系统工具 / 内置工具页的分类区块：上方标签，下方图标网格。</summary>
public sealed class LauncherSection
{
    public LauncherSection(string title, IReadOnlyList<LauncherEntry> entries)
    {
        Title = title;
        Entries = entries;
    }

    public string Title { get; }
    public IReadOnlyList<LauncherEntry> Entries { get; }
}

/// <summary>启动器里的一张图标卡片。</summary>
public sealed partial class LauncherEntry : ObservableObject
{
    public required ShortcutKind Kind { get; init; }
    public required int Index { get; init; }
    public required string Name { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIcon))]
    [NotifyPropertyChangedFor(nameof(ShowsKindGlyph))]
    private string _iconPath = string.Empty;

    public string TargetPath { get; init; } = string.Empty;

    /// <summary>开始菜单条目的 .lnk / .url 路径；其它类型为空。</summary>
    public string LinkPath { get; init; } = string.Empty;

    /// <summary>开始菜单条目的原始索引记录；收进启动器时使用。</summary>
    public StartMenuItem? StartMenuSource { get; init; }

    /// <summary>布局里的稳定标识：普通条目为 app|路径 / folder|路径 / web|url，系统工具为工具键。</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>启动器文件夹的 id；普通条目为空。</summary>
    public string GroupId { get; init; } = string.Empty;

    /// <summary>启动器文件夹内包含的卡片；普通条目为空。</summary>
    public IReadOnlyList<LauncherEntry> GroupItems { get; init; } = [];

    /// <summary>固定工具架（系统工具 / 内置工具）：只读，不参与排序、拖动与分组。</summary>
    public bool IsReadOnlyGroup { get; init; }

    /// <summary>系统工具的分类（如「系统管理」）；其它类型为空。</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>内置工具彩色图标的底色；为空时回退到种类字形。</summary>
    public Brush? IconBrush { get; init; }

    /// <summary>内置工具彩色图标里的白色字形（Segoe MDL2 Assets）。</summary>
    public string IconGlyph { get; init; } = string.Empty;

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
        ShortcutKind.StartMenu => "",
        ShortcutKind.Group => "",
        _ => "",
    };

    public string KindText => Kind switch
    {
        ShortcutKind.Folder => "文件夹",
        ShortcutKind.Web => "网站",
        ShortcutKind.Tool => string.IsNullOrEmpty(Category) ? "系统工具" : Category,
        ShortcutKind.InternalTool => string.IsNullOrEmpty(Category) ? "内置工具" : Category,
        ShortcutKind.StartMenu => string.IsNullOrEmpty(Category) ? "开始菜单" : Category,
        ShortcutKind.Group => $"{GroupItems.Count} 项",
        _ => "应用",
    };

    /// <summary>图标文件存在时才显示图片，否则回退到彩色图标或 KindGlyph；文件夹磁贴用 2×2 预览。</summary>
    public bool HasIcon => !IsGroup && !string.IsNullOrWhiteSpace(IconPath) && File.Exists(IconPath);

    /// <summary>内置工具用彩色圆角底 + 白色字形，和其它条目的图片图标区分开。</summary>
    public bool HasToolBadge => !IsGroup && IconBrush is not null && !string.IsNullOrEmpty(IconGlyph);

    /// <summary>既没有图片也没有彩色图标时才显示灰色的种类字形。</summary>
    public bool ShowsKindGlyph => !HasIcon && !HasToolBadge;

    public bool Matches(string query) =>
        Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || TargetPath.Contains(query, StringComparison.OrdinalIgnoreCase)
        || KindText.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Category.Contains(query, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// 启动器（Launchpad 样式）的视图模型：页签为本地应用 / 文件夹 / 网站 / 系统工具 / 内置工具 / 开始菜单。
/// 后三页只读；系统工具与内置工具直接在对应页签网格展示，不再走子文件夹浮层。
/// 支持搜索、页签切换、添加、改名、刷新图标、移除、自定义排序、多选与启动器文件夹。
/// </summary>
public sealed partial class ShortcutLauncherViewModel : ObservableObject
{
    private const int AppsPage = 0;
    private const int FoldersPage = 1;
    private const int WebsPage = 2;
    private const int SystemToolsPage = 3;
    private const int BuiltinToolsPage = 4;
    private const int StartMenuPage = 5;

    /// <summary>系统工具的分类排序；目录里新增分类时排在已知分类之后。</summary>
    private static readonly string[] ToolCategoryOrder =
        ["系统管理", "硬件与设备", "网络与安全", "性能与监控"];

    /// <summary>内置工具的分类排序。</summary>
    private static readonly string[] BuiltinCategoryOrder =
        ["环境", "监控", "窗口入口", "实用工具"];

    /// <summary>内置工具里直接唤回宿主窗口的三个入口 key。</summary>
    private const string TranslatorKey = "translator";
    private const string ClipboardKey = "clipboard";
    private const string BrowserKey = "browser";

    /// <summary>宿主内置工具：tool id、显示名、分类、说明、彩色图标字形与底色。</summary>
    private static readonly (string Key, string Name, string Category, string Description, string Glyph, string Color)[]
        BuiltinToolDefinitions =
    [
        ("devenv", "开发环境", "环境", "自定义后台服务启停", "\uE756", "#0F766E"),
        ("lava", "熔岩环境", "环境", "绿色版进程与软件管理", "\uE945", "#C2410C"),
        ("network", "网络监控", "监控", "按进程查看连接与速率", "\uE968", "#2563EB"),
        ("processes", "进程监控", "监控", "CPU / 内存与结束进程", "\uE9D9", "#7C3AED"),
        ("services", "服务监控", "监控", "Windows 服务启停与启动类型", "\uE9F5", "#0891B2"),
        ("ports", "端口管理", "监控", "查看占用并结束进程", "\uE8F2", "#0369A1"),
        ("speed", "网络测速", "监控", "延迟、下载与上传测速", "\uE9A6", "#CA8A04"),
        (TranslatorKey, "熔岩翻译", "窗口入口", "翻译剪贴板文本", "\uE774", "#EA580C"),
        (ClipboardKey, "熔岩剪贴板", "窗口入口", "剪贴板历史与图片", "\uE8C8", "#7C3AED"),
        (BrowserKey, "文件浏览器", "窗口入口", "目录导航与内嵌预览", "\uE8B7", "#D97706"),
        ("json", "JSON 格式化器", "实用工具", "格式化与校验 JSON", "\uE943", "#0891B2"),
        ("hosts", "Hosts 文件编辑器", "实用工具", "编辑系统 hosts 解析记录", "\uE968", "#DC2626"),
        ("image", "图像格式转换器", "实用工具", "图像格式转换与压缩", "\uE91B", "#16A34A"),
        ("hash", "文件哈希计算器", "实用工具", "计算文件校验值", "\uE8EF", "#475569"),
        ("codec", "编码工具箱", "实用工具", "编解码 / 加解密 / 哈希 / 时间", "\uE8F1", "#4F46E5"),
        ("file-tools", "文件解锁与粉碎", "实用工具", "解除占用与安全粉碎", "\uE785", "#0369A1"),
        ("startup", "启动项管理", "实用工具", "查看与管理开机启动项", "\uE7E8", "#CA8A04"),
        ("context-menu", "右键菜单管理", "实用工具", "自定义资源管理器右键菜单", "\uE700", "#6B7280"),
    ];

    /// <summary>固定色值转成冻结的画刷，避免每次绑定都新建一遍。</summary>
    private static SolidColorBrush CreateBadgeBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }

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

    /// <summary>开始菜单页的数据：索引结果与按一级文件夹分组的磁贴，不参与 LauncherLayout。</summary>
    private IReadOnlyList<StartMenuItem> _startMenuItems = [];
    private List<LauncherEntry> _startMenuTiles = [];
    private bool _startMenuLoading;
    private bool _startMenuReloadRequested;
    private string? _startMenuError;
    private CancellationTokenSource? _startMenuIconCts;
    private int _reloadGeneration;
    private int _refreshGeneration;

    /// <summary>快捷方式数据（应用 / 文件夹 / 网站）是否正在后台加载。</summary>
    private bool _shortcutDataLoading;

    /// <summary>Reload 期间请求的页签，数据落定后补上，避免被异步重建清掉。</summary>
    private int? _pendingSelectedPage;

    /// <summary>批量重建时暂时屏蔽属性变更回调里的 Refresh，避免重复刷新。</summary>
    private bool _suspendRefresh;

    public ShortcutLauncherViewModel() => Reload();

    public bool IsShortcutDataLoading => _shortcutDataLoading;

    public IReadOnlyList<LauncherPage> Pages { get; } =
    [
        new(AppsPage, "本地应用", ""),
        new(FoldersPage, "文件夹", ""),
        new(WebsPage, "网站", ""),
        new(SystemToolsPage, "系统工具", ""),
        new(BuiltinToolsPage, "内置工具", ""),
        new(StartMenuPage, "开始菜单", ""),
    ];

    public ObservableCollection<LauncherEntry> VisibleEntries { get; } = [];

    /// <summary>
    /// 当前页签（或搜索结果）的顶层卡片。文件夹以浮层卡片展开时，底层网格仍绑定这一份，
    /// 保证浮层后面还是原来的页签，而不是一片空白。
    /// </summary>
    public ObservableCollection<LauncherEntry> PageEntries { get; } = [];

    /// <summary>系统工具 / 内置工具页按分类分组后的区块（非搜索态）。</summary>
    public ObservableCollection<LauncherSection> ToolSections { get; } = [];

    /// <summary>
    /// 系统工具与内置工具在非搜索态用「分类标签 + 图标」布局；
    /// 搜索 / 其它页签仍用扁平网格。
    /// </summary>
    public bool ShowsToolSections =>
        !IsSearching && !IsInsideGroup && (IsSystemToolsPage || IsBuiltinToolsPage);

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

    /// <summary>搜索时的结果条数或文件夹内的条目数，显示在网格上方。</summary>
    [ObservableProperty]
    private string _summary = string.Empty;

    /// <summary>操作失败时的提示，显示在底部中间。</summary>
    [ObservableProperty]
    private string _message = string.Empty;

    /// <summary>操作成功后的中性提示（如「已添加到启动器」），显示在底部中间。</summary>
    [ObservableProperty]
    private string _notice = string.Empty;

    /// <summary>空状态标题，随当前页切换（开始菜单页与其它页文案不同）。</summary>
    [ObservableProperty]
    private string _emptyTitle = "这里还没有快捷方式";

    /// <summary>空状态补充说明，随当前页切换。</summary>
    [ObservableProperty]
    private string _emptyHint = "把 .exe 或文件夹拖进来，或点右下角 ＋ 添加";

    /// <summary>当前展开的启动器文件夹；为空表示在普通页签。</summary>
    [ObservableProperty]
    private LauncherEntry? _currentGroup;

    public bool IsInsideGroup => CurrentGroup is not null;

    /// <summary>当前展开的是不是固定工具架（系统工具 / 内置工具）：只读，不能框选、拖动、分组或移除。</summary>
    public bool IsInsideReadOnlyGroup => CurrentGroup?.IsReadOnlyGroup == true;

    /// <summary>当前是不是只读的开始菜单页。</summary>
    public bool IsStartMenuPage => SelectedPage == StartMenuPage;

    public bool IsSystemToolsPage => SelectedPage == SystemToolsPage;

    public bool IsBuiltinToolsPage => SelectedPage == BuiltinToolsPage;

    /// <summary>本地应用 / 文件夹 / 网站以外的页签只读。</summary>
    public bool IsReadOnlyLauncherPage => IsStartMenuPage || IsSystemToolsPage || IsBuiltinToolsPage;

    /// <summary>开始菜单正在后台读取；页面显示「正在读取开始菜单…」而不是空状态。</summary>
    public bool IsStartMenuLoading => _startMenuLoading;

    /// <summary>只读页签不接受外部文件拖入。</summary>
    public bool CanAcceptExternalDrop => IsEditableLauncherPage;

    /// <summary>当前是不是可编辑的页签（本地应用 / 文件夹 / 网站）。</summary>
    public bool IsEditableLauncherPage => SelectedPage is AppsPage or FoldersPage or WebsPage;

    /// <summary>当前视图是否允许框选、拖动、排序与分组。</summary>
    public bool IsEditableView => !IsSearching && !IsInsideReadOnlyGroup && IsEditableLauncherPage;

    /// <summary>只有可编辑页签能创建新文件夹。</summary>
    public bool CanCreateGroup => !IsSearching && !IsInsideGroup && IsEditableLauncherPage;

    /// <summary>只有可编辑页签显示右下角「添加」。</summary>
    public bool CanAddOnCurrentPage => !IsInsideGroup && IsEditableLauncherPage;

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

        Notice = string.Empty;
        OnPropertyChanged(nameof(IsStartMenuPage));
        OnPropertyChanged(nameof(IsSystemToolsPage));
        OnPropertyChanged(nameof(IsBuiltinToolsPage));
        OnPropertyChanged(nameof(IsReadOnlyLauncherPage));
        OnPropertyChanged(nameof(IsEditableLauncherPage));
        OnPropertyChanged(nameof(CanAcceptExternalDrop));
        OnPropertyChanged(nameof(ShowsToolSections));
        UpdateEmptyText();
        NotifyEditableChanged();
        if (value == StartMenuPage)
            BeginStartMenuLoad(force: false);
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

    partial void OnIsSearchingChanged(bool value) => OnPropertyChanged(nameof(ShowsToolSections));

    partial void OnCurrentGroupChanged(LauncherEntry? value)
    {
        OnPropertyChanged(nameof(IsInsideGroup));
        OnPropertyChanged(nameof(ShowsToolSections));
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

    /// <summary>
    /// 重新读取本地应用 / 文件夹 / 网站与两个工具文件夹；启动器每次打开都调用，保证其他入口的改动可见。
    /// 数据在后台线程读取，图标分批补齐；<paramref name="forceStartMenuRefresh"/> 为 true 时重新扫描开始菜单。
    /// </summary>
    public void Reload(bool forceStartMenuRefresh = false) => _ = ReloadAsync(forceStartMenuRefresh);

    private async Task ReloadAsync(bool forceStartMenuRefresh)
    {
        var generation = Interlocked.Increment(ref _reloadGeneration);
        SetShortcutDataLoading(true);

        ShortcutReloadPayload payload;
        try
        {
            payload = await Task.Run(BuildShortcutPayload).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (generation == _reloadGeneration)
            {
                await RunOnUiAsync(() => Message = ex.Message).ConfigureAwait(false);
                SetShortcutDataLoading(false);
            }
            return;
        }

        if (generation != _reloadGeneration)
            return;

        await RunOnUiAsync(() =>
        {
            ApplyShortcutPayload(payload);
            _suspendRefresh = false;
            NotifyEditableChanged();
            UpdateEmptyText();
            if (forceStartMenuRefresh || _startMenuItems.Count == 0)
                BeginStartMenuLoad(forceStartMenuRefresh);
            else
            {
                RebuildStartMenuTiles();
                RebuildAllItems();
            }
            Refresh();
            SetShortcutDataLoading(false);
        }).ConfigureAwait(false);

        if (generation != _reloadGeneration)
            return;

        _ = UpgradeAppIconsAsync(generation);
    }

    private static ShortcutReloadPayload BuildShortcutPayload()
    {
        var apps = CoreServices.Shortcuts.GetApps()
            .Select((item, index) => new LauncherEntry
            {
                Kind = ShortcutKind.App,
                Index = index,
                Key = LauncherLayout.AppKey(item.ExecPath),
                Name = item.Name,
                IconPath = item.IconPath ?? string.Empty,
                TargetPath = item.ExecPath,
            })
            .ToList();

        var folders = CoreServices.Shortcuts.GetFolders()
            .Select((item, index) => new LauncherEntry
            {
                Kind = ShortcutKind.Folder,
                Index = index,
                Key = LauncherLayout.FolderKey(item.FolderPath),
                Name = item.Name,
                IconPath = item.IconPath ?? string.Empty,
                TargetPath = item.FolderPath,
            })
            .ToList();

        var webs = CoreServices.Shortcuts.GetWebs()
            .Select((item, index) => new LauncherEntry
            {
                Kind = ShortcutKind.Web,
                Index = index,
                Key = LauncherLayout.WebKey(item.Url),
                Name = item.Name,
                IconPath = item.IconPath ?? string.Empty,
                TargetPath = item.Url,
            })
            .ToList();

        var tools = CoreServices.Shortcuts.GetSystemTools()
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
                IconPath = item.IconPath ?? string.Empty,
                TargetPath = item.Description,
            })
            .ToList();

        var builtinTools = BuiltinToolDefinitions
            .Select((item, index) => new LauncherEntry
            {
                Kind = ShortcutKind.InternalTool,
                Index = index,
                Key = item.Key,
                Name = item.Name,
                Category = item.Category,
                TargetPath = item.Description,
                IconGlyph = item.Glyph,
                IconBrush = CreateBadgeBrush(item.Color),
            })
            .ToList();

        var layout = CoreServices.Shortcuts.ReadLauncherLayout();
        layout.Normalize(
        [
            apps.Select(entry => entry.Key).ToList(),
            folders.Select(entry => entry.Key).ToList(),
            webs.Select(entry => entry.Key).ToList(),
        ]);

        return new ShortcutReloadPayload(apps, folders, webs, tools, builtinTools, layout);
    }

    private void ApplyShortcutPayload(ShortcutReloadPayload payload)
    {
        _suspendRefresh = true;
        CurrentGroup = null;
        _itemsByKey.Clear();

        _apps = payload.Apps;
        _folders = payload.Folders;
        _webs = payload.Webs;
        _tools = payload.Tools;
        _builtinTools = payload.BuiltinTools;

        foreach (var entry in _apps.Concat(_folders).Concat(_webs).Concat(_tools).Concat(_builtinTools))
            _itemsByKey[entry.Key] = entry;

        _layout = payload.Layout;
        RebuildTiles();
        RebuildAllItems();

        if (_pendingSelectedPage is int pendingPage)
        {
            SelectedPage = pendingPage;
            _pendingSelectedPage = null;
        }
    }

    private sealed record ShortcutReloadPayload(
        List<LauncherEntry> Apps,
        List<LauncherEntry> Folders,
        List<LauncherEntry> Webs,
        List<LauncherEntry> Tools,
        List<LauncherEntry> BuiltinTools,
        LauncherLayout Layout);

    /// <summary>把应用 / 文件夹 / 网站 / 工具与开始菜单条目合并成搜索数据源。</summary>
    private void RebuildAllItems()
    {
        _allItems = _apps
            .Concat(_folders)
            .Concat(_webs)
            .Concat(_tools)
            .Concat(_builtinTools)
            .Concat(_startMenuTiles.Where(entry => !entry.IsGroup))
            .Concat(_startMenuTiles.Where(entry => entry.IsGroup).SelectMany(entry => entry.GroupItems))
            .ToList();
    }

    /// <summary>按一级文件夹重建开始菜单页；已经收进启动器的条目在这里被过滤掉。</summary>
    private void RebuildStartMenuTiles()
    {
        var launcherPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _apps.Concat(_folders))
            launcherPaths.Add(StartMenuIndex.NormalizePathForCompare(entry.TargetPath));

        var launcherUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _webs)
            launcherUrls.Add(StartMenuIndex.NormalizeUrlForCompare(entry.TargetPath));

        var visible = _startMenuItems
            .Where(item => !StartMenuIndex.ShouldHide(item, launcherPaths, launcherUrls))
            .ToList();

        var tiles = new List<LauncherEntry>();

        foreach (var group in visible
            .Where(item => !string.IsNullOrWhiteSpace(item.FirstLevelFolder))
            .GroupBy(item => item.FirstLevelFolder, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var children = group
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
                .Select(BuildStartMenuEntry)
                .ToList();
            if (children.Count == 0)
                continue;

            tiles.Add(new LauncherEntry
            {
                Kind = ShortcutKind.Group,
                Index = -1,
                Key = $"startmenu-group|{group.Key}",
                GroupId = $"startmenu|{group.Key}",
                Name = group.Key,
                GroupItems = children,
                IsReadOnlyGroup = true,
            });
        }

        foreach (var item in visible
            .Where(item => string.IsNullOrWhiteSpace(item.FirstLevelFolder))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            tiles.Add(BuildStartMenuEntry(item));
        }

        _startMenuTiles = tiles;
    }

    private static LauncherEntry BuildStartMenuEntry(StartMenuItem item) => new()
    {
        Kind = ShortcutKind.StartMenu,
        Index = -1,
        Key = item.Key,
        Name = item.Name,
        IconPath = item.IconPath,
        TargetPath = string.IsNullOrWhiteSpace(item.TargetPath) ? item.LinkPath : item.TargetPath,
        LinkPath = item.LinkPath,
        Category = BuildStartMenuCategory(item),
        StartMenuSource = item,
    };

    /// <summary>搜索时显示的开始菜单目录：一级文件夹 + 更深的子目录。</summary>
    private static string BuildStartMenuCategory(StartMenuItem item)
    {
        if (string.IsNullOrWhiteSpace(item.SubFolder))
            return item.FirstLevelFolder;
        if (string.IsNullOrWhiteSpace(item.FirstLevelFolder))
            return item.SubFolder;
        return $"{item.FirstLevelFolder} / {item.SubFolder}";
    }

    /// <summary>后台读取开始菜单；已有请求在跑时不重复启动，必要时完成后补一次强制刷新。</summary>
    private void BeginStartMenuLoad(bool force)
    {
        if (!force && _startMenuItems.Count > 0)
        {
            if (!CoreServices.StartMenu.IconsFullyEnriched)
                ContinueStartMenuIcons();
            return;
        }

        if (_startMenuLoading)
        {
            if (force)
                _startMenuReloadRequested = true;
            return;
        }

        _ = LoadStartMenuAsync(force);
    }

    private async Task LoadStartMenuAsync(bool force)
    {
        _startMenuLoading = true;
        _startMenuError = null;
        _startMenuIconCts?.Cancel();
        _startMenuIconCts = new CancellationTokenSource();
        var iconCt = _startMenuIconCts.Token;

        try
        {
            _startMenuItems = await Task.Run(() => CoreServices.StartMenu.GetItems(force)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _startMenuError = ex.Message;
        }
        finally
        {
            _startMenuLoading = false;
        }

        var groupId = CurrentGroup?.GroupId;
        var wasStartMenuGroup = CurrentGroup?.Key.StartsWith("startmenu-group|", StringComparison.OrdinalIgnoreCase) == true;

        await RunOnUiAsync(() =>
        {
            RebuildStartMenuTiles();
            RebuildAllItems();

            if (wasStartMenuGroup && groupId is not null)
            {
                _suspendRefresh = true;
                CurrentGroup = FindStartMenuGroup(groupId);
                _suspendRefresh = false;
            }

            OnPropertyChanged(nameof(IsStartMenuLoading));
            Refresh();
        }).ConfigureAwait(false);

        if (_startMenuReloadRequested)
        {
            _startMenuReloadRequested = false;
            BeginStartMenuLoad(force: true);
            return;
        }

        _ = EnrichStartMenuIconsAsync(iconCt);
    }

    private void ContinueStartMenuIcons()
    {
        if (CoreServices.StartMenu.IconsFullyEnriched)
            return;

        _startMenuIconCts?.Cancel();
        _startMenuIconCts = new CancellationTokenSource();
        _ = EnrichStartMenuIconsAsync(_startMenuIconCts.Token);
    }

    private async Task EnrichStartMenuIconsAsync(CancellationToken cancellationToken)
    {
        var index = 0;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var next = await Task.Run(
                        () => CoreServices.StartMenu.EnrichIconsBatch(index, 24),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (next <= index)
                    break;

                index = next;
                await RunOnUiAsync(SyncStartMenuIcons).ConfigureAwait(false);

                if (next >= _startMenuItems.Count)
                    break;

                await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // 新一轮加载会自行取消上一轮
        }
    }

    /// <summary>开始菜单图标分批落盘后，同步到已生成的卡片（含文件夹内的子项）。</summary>
    private void SyncStartMenuIcons()
    {
        foreach (var entry in EnumerateStartMenuEntries())
        {
            if (entry.StartMenuSource is null)
                continue;

            var path = entry.StartMenuSource.IconPath;
            if (string.Equals(entry.IconPath, path, StringComparison.OrdinalIgnoreCase))
                continue;

            entry.IconPath = path;
        }
    }

    private IEnumerable<LauncherEntry> EnumerateStartMenuEntries()
    {
        foreach (var tile in _startMenuTiles)
        {
            if (tile.IsGroup)
            {
                foreach (var child in tile.GroupItems)
                    yield return child;
            }
            else
            {
                yield return tile;
            }
        }
    }

    private LauncherEntry? FindStartMenuGroup(string groupId) =>
        _startMenuTiles.FirstOrDefault(entry => entry.IsGroup
            && string.Equals(entry.GroupId, groupId, StringComparison.OrdinalIgnoreCase));

    /// <summary>空状态文案随页签切换：开始菜单页不显示「拖入 .exe」这类添加提示。</summary>
    private void UpdateEmptyText()
    {
        if (IsStartMenuPage)
        {
            EmptyTitle = "开始菜单里没有可显示的项";
            EmptyHint = "系统开始菜单中的快捷方式会显示在这里；已加入启动器的条目会自动隐藏";
        }
        else if (IsSystemToolsPage)
        {
            EmptyTitle = "没有可用的系统工具";
            EmptyHint = "Windows 管理工具会显示在这里";
        }
        else if (IsBuiltinToolsPage)
        {
            EmptyTitle = "没有可用的内置工具";
            EmptyHint = "宿主内置小工具会显示在这里";
        }
        else
        {
            EmptyTitle = "这里还没有快捷方式";
            EmptyHint = "把 .exe 或文件夹拖进来，或点右下角 ＋ 添加";
        }
    }

    /// <summary>后台把应用图标升到启动器需要的 64px，结果写回卡片，不阻塞打开启动器。</summary>
    private async Task UpgradeAppIconsAsync(int generation)
    {
        var apps = _apps.ToList();
        foreach (var entry in apps)
        {
            if (generation != _reloadGeneration)
                return;

            var execPath = entry.TargetPath;
            var currentPath = entry.IconPath;
            var upgraded = await Task.Run(() => IconHelper.EnsureLargeIcon(execPath, currentPath))
                .ConfigureAwait(false);
            if (generation != _reloadGeneration)
                return;

            var path = upgraded ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path)
                || string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            await RunOnUiAsync(() =>
            {
                if (generation != _reloadGeneration)
                    return;

                FilePathToImageConverter.Invalidate(currentPath);
                FilePathToImageConverter.Invalidate(path);
                entry.IconPath = path;
            }).ConfigureAwait(false);
        }
    }

    private void SetShortcutDataLoading(bool loading)
    {
        if (_shortcutDataLoading == loading)
            return;

        _shortcutDataLoading = loading;
        OnPropertyChanged(nameof(IsShortcutDataLoading));
    }

    private static Task RunOnUiAsync(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action, DispatcherPriority.Background).Task;
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

        IEnumerable<LauncherEntry> pageSource = IsSearching
            ? _allItems.Where(entry => entry.Matches(query))
            : GetPageEntries(SelectedPage);

        var pageEntries = pageSource.ToList();
        ReplaceCollectionBatched(PageEntries, pageEntries);

        // 文件夹展开时，可见条目换成组内条目；底层页签仍保留在 PageEntries 里
        IEnumerable<LauncherEntry> source = CurrentGroup is not null ? CurrentGroup.GroupItems : pageEntries;
        ReplaceCollectionBatched(VisibleEntries, source.ToList());

        RebuildToolSections(pageEntries);

        var startMenuPending = IsStartMenuPage && _startMenuLoading && _startMenuTiles.Count == 0;
        IsEmpty = !IsInsideGroup && VisibleEntries.Count == 0 && !startMenuPending;
        Summary = IsSearching
            ? $"{VisibleEntries.Count} 个匹配项"
            : IsInsideGroup
                ? $"{VisibleEntries.Count} 项"
                : IsStartMenuPage
                    ? startMenuPending
                        ? "正在读取开始菜单…"
                        : !string.IsNullOrEmpty(_startMenuError) && _startMenuTiles.Count == 0
                            ? "开始菜单读取失败"
                            : $"{_startMenuTiles.Sum(entry => entry.IsGroup ? entry.GroupItems.Count : 1)} 项"
                    : IsSystemToolsPage
                        ? $"{_tools.Count} 项"
                        : IsBuiltinToolsPage
                            ? $"{_builtinTools.Count} 项"
                            : string.Empty;

        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedEntries));
        OnPropertyChanged(nameof(ShowsToolSections));
    }

    /// <summary>把系统 / 内置工具按 Category 拆成「标签 + 图标」区块；其它页签清空。</summary>
    private void RebuildToolSections(IReadOnlyList<LauncherEntry> pageEntries)
    {
        ToolSections.Clear();
        if (!ShowsToolSections)
            return;

        var order = IsBuiltinToolsPage ? BuiltinCategoryOrder : ToolCategoryOrder;
        foreach (var section in BuildSections(pageEntries, order))
            ToolSections.Add(section);
    }

    private static IEnumerable<LauncherSection> BuildSections(
        IReadOnlyList<LauncherEntry> entries,
        string[] categoryOrder)
    {
        return entries
            .GroupBy(entry => string.IsNullOrWhiteSpace(entry.Category) ? "其它" : entry.Category)
            .OrderBy(group =>
            {
                var rank = Array.IndexOf(categoryOrder, group.Key);
                return rank < 0 ? int.MaxValue : rank;
            })
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new LauncherSection(group.Key, group.ToList()));
    }

    private IReadOnlyList<LauncherEntry> GetPageEntries(int page) => page switch
    {
        StartMenuPage => _startMenuTiles,
        SystemToolsPage => _tools,
        BuiltinToolsPage => _builtinTools,
        >= 0 and < LauncherLayout.EditablePageCount => _pageTiles[page],
        _ => [],
    };

    private const int CollectionBatchThreshold = 40;
    private const int CollectionBatchSize = 24;

    private void ReplaceCollectionBatched(
        ObservableCollection<LauncherEntry> target,
        IReadOnlyList<LauncherEntry> entries)
    {
        var generation = Interlocked.Increment(ref _refreshGeneration);
        if (entries.Count <= CollectionBatchThreshold)
        {
            target.Clear();
            foreach (var entry in entries)
                target.Add(entry);
            return;
        }

        target.Clear();
        _ = PumpCollectionAsync(target, entries, generation);
    }

    private async Task PumpCollectionAsync(
        ObservableCollection<LauncherEntry> target,
        IReadOnlyList<LauncherEntry> entries,
        int generation)
    {
        for (var index = 0; index < entries.Count; index += CollectionBatchSize)
        {
            if (generation != _refreshGeneration)
                return;

            var slice = entries.Skip(index).Take(CollectionBatchSize).ToList();
            await RunOnUiAsync(() =>
            {
                if (generation != _refreshGeneration)
                    return;

                foreach (var entry in slice)
                    target.Add(entry);
            }).ConfigureAwait(false);

            if (index + CollectionBatchSize < entries.Count)
                await Task.Delay(1).ConfigureAwait(false);
        }
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

    /// <summary>切到「系统工具」页签（兼容 DEVENV_START_PAGE=tools 等路由）。</summary>
    public void OpenSystemToolsShelf() => NavigateToLauncherPage(SystemToolsPage);

    /// <summary>切到「内置工具」页签（兼容 DEVENV_START_PAGE=builtin 等路由）。</summary>
    public void OpenBuiltinToolsShelf() => NavigateToLauncherPage(BuiltinToolsPage);

    private void NavigateToLauncherPage(int page)
    {
        Message = string.Empty;
        Query = string.Empty;
        if (CurrentGroup is not null)
            CloseGroupWithoutRefresh();
        if (_shortcutDataLoading)
            _pendingSelectedPage = page;
        else
            SelectedPage = page;
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
        if (IsSearching || IsInsideGroup || !IsEditableLauncherPage)
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
        if (!group.IsGroup || !IsEditableLauncherPage)
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
        if (CurrentGroup is null || IsReadOnlyLauncherPage)
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
        if (!group.IsGroup || group.IsReadOnlyGroup || !IsEditableLauncherPage || string.IsNullOrWhiteSpace(name))
            return;

        _layout.RenameGroup(group.GroupId, name);
        Commit();
    }

    public void DissolveGroup(LauncherEntry group)
    {
        if (!group.IsGroup || group.IsReadOnlyGroup || !IsEditableLauncherPage)
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
        if (!IsEditableView)
            return;

        if (IsInsideGroup)
        {
            MoveGroupItems(entries, insertIndex);
            return;
        }

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
            ShortcutKind.StartMenu => CoreServices.StartMenu.OpenLink(entry.LinkPath),
            ShortcutKind.InternalTool => entry.Key switch
            {
                TranslatorKey => RunTranslator(),
                ClipboardKey => RunClipboard(),
                BrowserKey => RunBrowser(),
                _ => ToolWindowHost.Open(entry.Key),
            },
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
        else if (entry.Kind == ShortcutKind.StartMenu)
            Report(CoreServices.StartMenu.RevealLink(entry.LinkPath));
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

    /// <summary>把开始菜单页的一条快捷方式收进启动器。</summary>
    public void AddStartMenuEntry(LauncherEntry entry)
    {
        if (entry.StartMenuSource is null)
            return;

        var result = CoreServices.Shortcuts.AddFromStartMenu(entry.StartMenuSource);
        Report(result);
        if (!result.Ok)
            return;

        Notice = $"已添加到启动器：{entry.Name}";
        Reload();
    }

    /// <summary>底部「＋」按当前页签添加。</summary>
    public async Task AddForCurrentPageAsync(Window owner)
    {
        if (!IsEditableLauncherPage)
            return;

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
        if (!IsEditableLauncherPage)
            return null;

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
        if (entry.Kind == ShortcutKind.StartMenu || entry.IsReadOnlyGroup)
            return;

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
        if (entry.Kind == ShortcutKind.StartMenu || entry.IsReadOnlyGroup)
            return;

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
        if (!IsEditableLauncherPage)
            return;

        var selected = SelectedEntries
            .Where(entry => entry.Kind != ShortcutKind.StartMenu && !entry.IsReadOnlyGroup)
            .ToList();
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
        if (entry.IsGroup || entry.Kind == ShortcutKind.StartMenu)
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

    /// <summary>打开熔岩翻译并填充剪贴板文本，行为与 Alt+Space 翻译热键一致。</summary>
    private static OperationResult RunTranslator()
    {
        if (HostCallbacks.OnShowTranslator is not { } show)
            return OperationResult.Fail("翻译窗暂不可用");

        show(new TranslatorRequest { Visible = true, FillClipboard = true });
        return OperationResult.Success();
    }

    /// <summary>打开熔岩剪贴板窗口。</summary>
    private static OperationResult RunClipboard()
    {
        if (HostCallbacks.OnShowClipboard is not { } show)
            return OperationResult.Fail("剪贴板窗暂不可用");

        show(true);
        return OperationResult.Success();
    }

    /// <summary>打开 SigXor 文件浏览器（目录导航 + 内嵌预览）。</summary>
    private static OperationResult RunBrowser()
    {
        if (HostCallbacks.OnOpenSigXorBrowser is not { } show)
            return OperationResult.Fail("文件浏览器暂不可用");

        show();
        return OperationResult.Success();
    }
}
