using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBox.Core.Models;
using DevBox.Core.Services;
using DevBox.Host.Infrastructure;
using DevBox.Core.LavaEnv;
using DevBox.Core.LavaEnv.Models;

namespace DevBox.Host.ViewModels;

/// <summary>熔岩环境页：服务 / 软件下载 / 活动下载 / 下载历史 / 镜像 / 扫描 / 数据目录。</summary>
public sealed partial class LavaViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _serviceTimer;
    private readonly DispatcherTimer _downloadTimer;
    private readonly List<CatalogEntryViewModel> _allEntries = [];
    private string _recommendedMirrorKey = string.Empty;
    private bool _scanInProgress;

    public LavaViewModel()
    {
        Services = [];
        FilteredCatalog = [];
        ActiveDownloads = [];
        DownloadHistory = [];
        Mirrors = [];
        MirrorTools = [];
        ScanResults = [];

        _serviceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _serviceTimer.Tick += (_, _) => LoadServices();
        _serviceTimer.Start();

        _downloadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _downloadTimer.Tick += (_, _) => LoadDownloads();
        _downloadTimer.Start();

        LoadServices();
        LoadDataInfo();
        LoadCatalog();
    }

    #region 通用

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private int _selectedTabIndex;

    public IReadOnlyList<string> Tabs { get; } =
        ["服务", "软件下载", "活动下载", "下载历史", "镜像", "扫描", "数据目录"];

    private const int TabServices = 0;
    private const int TabDownloads = 1;
    private const int TabActive = 2;
    private const int TabMirrors = 4;

    public void Report(OperationResult result)
    {
        if (!string.IsNullOrEmpty(result.DisplayMessage))
            Message = result.DisplayMessage;
    }

    #endregion

    #region 服务（绿色版进程）

    public ObservableCollection<LavaProcessItem> Services { get; }

    [ObservableProperty]
    private string? _busyService;

    private void LoadServices()
    {
        var services = CoreServices.Lava.GetProcesses();
        Services.Clear();
        foreach (var item in services)
            Services.Add(item);
    }

    [RelayCommand]
    private async Task StartServiceAsync(LavaProcessItem? item)
    {
        if (item is null)
            return;
        BusyService = item.Name;
        try
        {
            Report(await CoreServices.Lava.StartProcessAsync(item.Name));
            LoadServices();
        }
        finally
        {
            BusyService = null;
        }
    }

    [RelayCommand]
    private async Task StopServiceAsync(LavaProcessItem? item)
    {
        if (item is null)
            return;
        BusyService = item.Name;
        try
        {
            Report(await CoreServices.Lava.StopProcessAsync(item.Name));
            LoadServices();
        }
        finally
        {
            BusyService = null;
        }
    }

    [RelayCommand]
    private void GoToDownloads(LavaProcessItem? item)
    {
        if (item is null)
            return;
        var keyword = string.IsNullOrWhiteSpace(item.DisplayName) ? item.Name : item.DisplayName;
        CatalogQuery = keyword;
        DownloadFilter = keyword;
        SelectedTabIndex = TabDownloads;
    }

    [RelayCommand]
    private void OpenAppsDirectory() => Report(CoreServices.Lava.OpenAppsDirectory());

    [RelayCommand]
    private void OpenDataRoot() => Report(CoreServices.Lava.OpenDataRoot());

    [RelayCommand]
    private void OpenConfigFile() => Report(CoreServices.Lava.OpenConfigFile());

    [RelayCommand]
    private void OpenCacheDirectory() => Report(CoreServices.Lava.OpenCacheDirectory());

    [RelayCommand]
    private void OpenEnvironmentVariables() => Report(CoreServices.Lava.OpenEnvironmentVariables());

    [RelayCommand]
    private void OpenDocs() => HostServices.OpenUrl(AppInfo.DocsUrl);

    [RelayCommand]
    private void OpenRepository() => HostServices.OpenUrl(AppInfo.RepositoryUrl);

    #endregion

    #region 软件下载

    /// <summary>目录条目（带当前选中的版本，替代 React 里的 selectedVersion 字典）。</summary>
    public sealed partial class CatalogEntryViewModel : ObservableObject
    {
        public CatalogEntryViewModel(string category, CatalogItem item)
        {
            Category = category;
            Item = item;
            SelectedVersion = item.Versions.FirstOrDefault()?.Version ?? string.Empty;
        }

        public string Category { get; }
        public CatalogItem Item { get; }

        [ObservableProperty]
        private string _selectedVersion;

        public IEnumerable<CatalogVersion> Versions => Item.Versions;

        public CatalogVersion? Selected => Item.Versions.FirstOrDefault(v => v.Version == SelectedVersion)
            ?? Item.Versions.FirstOrDefault();
    }

    /// <summary>目录分组（一行分类标题 + 若干条目）。</summary>
    public sealed class CatalogGroupViewModel
    {
        public CatalogGroupViewModel(string category, IEnumerable<CatalogEntryViewModel> entries)
        {
            Category = category;
            Entries = entries.ToList();
        }

        public string Category { get; }
        public IReadOnlyList<CatalogEntryViewModel> Entries { get; }
    }

    public ObservableCollection<CatalogGroupViewModel> FilteredCatalog { get; }

    public ObservableCollection<string> CatalogCategories { get; } = ["全部"];

    [ObservableProperty]
    private string _catalogQuery = string.Empty;

    [ObservableProperty]
    private string _catalogCategory = "全部";

    private void LoadCatalog()
    {
        _allEntries.Clear();
        CatalogCategories.Clear();
        CatalogCategories.Add("全部");

        foreach (var category in CoreServices.Lava.GetSoftwareCatalog())
        {
            if (!CatalogCategories.Contains(category.Category))
                CatalogCategories.Add(category.Category);
            foreach (var item in category.Items)
                _allEntries.Add(new CatalogEntryViewModel(category.Category, item));
        }

        RebuildCatalogFilter();
    }

    partial void OnCatalogQueryChanged(string value) => RebuildCatalogFilter();

    partial void OnCatalogCategoryChanged(string value) => RebuildCatalogFilter();

    private void RebuildCatalogFilter()
    {
        var query = CatalogQuery.Trim();
        var matches = _allEntries.Where(entry =>
        {
            if (CatalogCategory != "全部" && !string.Equals(entry.Category, CatalogCategory, StringComparison.Ordinal))
                return false;
            if (query.Length == 0)
                return true;
            return entry.Item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.Item.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
        });

        FilteredCatalog.Clear();
        foreach (var group in matches.GroupBy(e => e.Category))
            FilteredCatalog.Add(new CatalogGroupViewModel(group.Key, group));
    }

    [RelayCommand]
    private void ResetCatalogFilter()
    {
        CatalogQuery = string.Empty;
        CatalogCategory = "全部";
    }

    [RelayCommand]
    private async Task DownloadAsync(CatalogEntryViewModel? entry)
    {
        if (entry?.Selected is not { } version)
            return;

        Report(await CoreServices.Lava.StartDownloadAsync(
            version.Url, version.FileName, entry.Item.Name, version.Version));
        DownloadFilter = string.Empty;
        SelectedTabIndex = TabActive;
        LoadDownloads();
    }

    #endregion

    #region 下载（活动 / 历史）

    public ObservableCollection<DownloadItem> ActiveDownloads { get; }
    public ObservableCollection<DownloadItem> DownloadHistory { get; }

    [ObservableProperty]
    private DownloadStats? _downloadStats;

    [ObservableProperty]
    private string _downloadFilter = string.Empty;

    public IEnumerable<DownloadItem> FilteredHistory => string.IsNullOrWhiteSpace(DownloadFilter)
        ? DownloadHistory
        : DownloadHistory.Where(d => d.SoftwareName.Contains(DownloadFilter, StringComparison.OrdinalIgnoreCase));

    partial void OnDownloadFilterChanged(string value) => OnPropertyChanged(nameof(FilteredHistory));

    private void LoadDownloads()
    {
        var result = CoreServices.Lava.GetDownloads();
        DownloadStats = result.Stats;

        Replace(ActiveDownloads, result.Items.Where(d => d.IsActive));
        Replace(DownloadHistory, result.Items);
    }

    private static void Replace(ObservableCollection<DownloadItem> target, IEnumerable<DownloadItem> source)
    {
        target.Clear();
        foreach (var item in source)
            target.Add(item);
    }

    [RelayCommand]
    private void RefreshDownloads() => LoadDownloads();

    [RelayCommand]
    private void ClearDownloadFilter() => DownloadFilter = string.Empty;

    [RelayCommand]
    private void SelectDownloadsTab() => SelectedTabIndex = TabDownloads;

    [RelayCommand]
    private void CancelDownload(DownloadItem? item)
    {
        if (item is null)
            return;
        CoreServices.Lava.CancelDownload(item.Id);
        LoadDownloads();
    }

    [RelayCommand]
    private void OpenDownloadedFile(DownloadItem? item)
    {
        if (item is null)
            return;
        Report(CoreServices.Lava.OpenDownloadedFile(item.Id));
    }

    [RelayCommand]
    private void ClearCompleted()
    {
        CoreServices.Lava.ClearCompletedDownloads();
        LoadDownloads();
    }

    [RelayCommand]
    private void ClearFailed()
    {
        CoreServices.Lava.ClearFailedDownloads();
        LoadDownloads();
    }

    [RelayCommand]
    private void ClearAll()
    {
        if (!Windows.ConfirmDialog.Show(HostServices.DialogOwner, "确定清空全部下载历史？", "清空"))
            return;
        CoreServices.Lava.ClearAllDownloads();
        LoadDownloads();
    }

    #endregion

    #region 镜像

    public ObservableCollection<MirrorItem> Mirrors { get; }
    public ObservableCollection<MirrorToolOption> MirrorTools { get; }

    [ObservableProperty]
    private string _selectedMirrorTool = "Pip";

    [ObservableProperty]
    private string? _currentMirror;

    [ObservableProperty]
    private bool _testingMirrors;

    public bool IsRecommended(MirrorItem item) =>
        !string.IsNullOrEmpty(_recommendedMirrorKey) && item.Key == _recommendedMirrorKey;

    /// <summary>镜像当前值要跑子进程读 pip/npm 配置，切到该页再加载，别在页面构造时就付这个代价。</summary>
    public void OnTabChanged(int index)
    {
        if (index == TabMirrors)
            _ = LoadMirrors();
    }

    private async Task LoadMirrors()
    {
        try
        {
            var info = await CoreServices.Lava.GetMirrorsAsync(SelectedMirrorTool);
            CurrentMirror = info.Current;

            Mirrors.Clear();
            foreach (var mirror in info.Mirrors)
                Mirrors.Add(mirror);

            MirrorTools.Clear();
            foreach (var tool in info.Tools)
                MirrorTools.Add(tool);
        }
        catch (Exception ex)
        {
            Message = $"镜像加载失败：{ex.Message}";
        }
    }

    partial void OnSelectedMirrorToolChanged(string value) => _ = LoadMirrors();

    [RelayCommand]
    private Task RefreshMirrorsAsync() => LoadMirrors();

    [RelayCommand]
    private async Task TestMirrorsAsync()
    {
        TestingMirrors = true;
        try
        {
            var (result, test) = await CoreServices.Lava.TestMirrorsAsync(SelectedMirrorTool);
            Report(result);
            if (test is null)
                return;

            _recommendedMirrorKey = test.Recommended?.Key ?? string.Empty;
            Mirrors.Clear();
            foreach (var mirror in test.Mirrors)
                Mirrors.Add(mirror);

            Message = test.Recommended is null
                ? "测速完成，所有镜像均不可用"
                : $"测速完成，推荐：{test.Recommended.Name} ({Math.Round(test.Recommended.ResponseTimeMs ?? 0)} ms)";
        }
        finally
        {
            TestingMirrors = false;
        }
    }

    [RelayCommand]
    private async Task UseMirrorAsync(MirrorItem? item)
    {
        if (item is null)
            return;
        Report(await CoreServices.Lava.ConfigureMirrorAsync(SelectedMirrorTool, item.Key));
        await LoadMirrors();
    }

    #endregion

    #region 环境扫描

    public ObservableCollection<InstalledEnvironment> ScanResults { get; }

    [ObservableProperty]
    private bool _isScanning;

    public IEnumerable<IGrouping<string, InstalledEnvironment>> ScanSummary =>
        ScanResults.GroupBy(s => s.Type);

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (_scanInProgress)
            return;
        _scanInProgress = true;
        IsScanning = true;
        try
        {
            var results = await CoreServices.Lava.ScanAsync();
            ScanResults.Clear();
            foreach (var item in results)
                ScanResults.Add(item);
            OnPropertyChanged(nameof(ScanSummary));
            Message = $"扫描完成：{results.Count} 项";
        }
        catch (Exception ex)
        {
            Message = $"扫描失败：{ex.Message}";
        }
        finally
        {
            _scanInProgress = false;
            IsScanning = false;
        }
    }

    #endregion

    #region 数据目录

    [ObservableProperty]
    private LavaDataInfo? _dataInfo;

    private void LoadDataInfo() => DataInfo = CoreServices.Lava.GetDataInfo();

    [RelayCommand]
    private void RefreshDataInfo() => LoadDataInfo();

    #endregion

    public void Dispose()
    {
        _serviceTimer.Stop();
        _downloadTimer.Stop();
    }
}