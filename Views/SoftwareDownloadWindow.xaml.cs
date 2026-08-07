using DevEnv.UI;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DevEnv.Models;
using DevEnv.Services;

namespace DevEnv.Views;

public partial class SoftwareDownloadWindow : Window
{
    private readonly ObservableCollection<SoftwareInfo> _allSoftware = new();
    private readonly ObservableCollection<SoftwareInfo> _filteredSoftware = new();
    private readonly List<string> _categories = [];

    public SoftwareDownloadWindow()
    {
        InitializeComponent();
        LoadSoftwareConfig();
        AppServices.DownloadHistory.HistoryChanged += (_, _) =>
            Dispatcher.UIThread.Post(UpdateActiveDownloadCount);
    }

    private void LoadSoftwareConfig()
    {
        try
        {
            var config = ResourceLoader.LoadBundledJson<Dictionary<string, List<SoftwareCatalogItem>>>(
                ResourcePaths.SoftwareCatalogFile);

            if (config == null || config.Count == 0)
            {
                UpdateStatus($"资源文件不存在或为空: {ResourcePaths.GetBundledResourcePath(ResourcePaths.SoftwareCatalogFile)}", true);
                return;
            }

            _allSoftware.Clear();
            _categories.Clear();
            _categories.Add("全部");

            foreach (var (category, items) in config)
            {
                _categories.Add(category);

                foreach (var item in items)
                {
                    var softwareInfo = new SoftwareInfo
                    {
                        Name = item.Name,
                        Description = item.Desc,
                        Icon = item.Icon,
                        Category = category,
                        Versions = []
                    };

                    foreach (var version in item.Versions)
                    {
                        var fileName = version.FileName;
                        if (string.IsNullOrEmpty(fileName))
                            fileName = InferFileName(version.Url, item.Name, version.Version);

                        softwareInfo.Versions.Add(new SoftwareVersion
                        {
                            Version = version.Version,
                            Url = version.Url,
                            FileName = fileName
                        });
                    }

                    softwareInfo.SelectedVersion = softwareInfo.Versions.FirstOrDefault();
                    _allSoftware.Add(softwareInfo);
                }
            }

            CmbCategory.ItemsSource = _categories;
            CmbCategory.SelectedIndex = 0;

            _filteredSoftware.Clear();
            foreach (var software in _allSoftware)
                _filteredSoftware.Add(software);

            SoftwareDataGrid.ItemsSource = _filteredSoftware;
            UpdateStatus($"已加载 {_allSoftware.Count} 个软件", false);
            UpdateCount();
            UpdateActiveDownloadCount();
        }
        catch (Exception ex)
        {
            UpdateStatus($"加载配置失败: {ex.Message}", true);
        }
    }

    private static string InferFileName(string url, string softwareName, string version)
    {
        if (string.IsNullOrEmpty(url)) return $"{softwareName}_{version}.exe";
        try
        {
            var uri = new Uri(url);
            var name = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrEmpty(name) && name != "/")
                return name;
        }
        catch
        {
            // ignore
        }

        return $"{softwareName}_{version}{Path.GetExtension(url)}";
    }

    private void TxtSearch_TextChanged(object? sender, TextChangedEventArgs e) => FilterSoftware();
    private void CmbCategory_SelectionChanged(object? sender, SelectionChangedEventArgs e) => FilterSoftware();
    private void BtnSearch_Click(object? sender, RoutedEventArgs e) => FilterSoftware();

    private void BtnReset_Click(object? sender, RoutedEventArgs e)
    {
        TxtSearch.Text = "";
        CmbCategory.SelectedIndex = 0;
        FilterSoftware();
    }

    private void FilterSoftware()
    {
        var searchText = TxtSearch.Text?.ToLower() ?? "";
        var selectedCategory = CmbCategory.SelectedItem?.ToString() ?? "全部";

        var filtered = _allSoftware.Where(s =>
            (selectedCategory == "全部" || s.Category == selectedCategory) &&
            (string.IsNullOrWhiteSpace(searchText) ||
             s.Name.ToLower().Contains(searchText) ||
             s.Description.ToLower().Contains(searchText))
        ).ToList();

        _filteredSoftware.Clear();
        foreach (var software in filtered)
            _filteredSoftware.Add(software);

        UpdateStatus($"找到 {filtered.Count} 个软件", false);
        UpdateCount();
    }

    private async void BtnDownload_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not SoftwareInfo software)
            return;

        var version = software.SelectedVersion ?? software.Versions.FirstOrDefault();

        if (version == null || string.IsNullOrWhiteSpace(version.Url))
        {
            MessageBox.Show("请先选择版本", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await StartDownloadAsync(software, version, button);
    }

    private static void VersionComboBox_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private async Task StartDownloadAsync(SoftwareInfo software, SoftwareVersion version, Button button)
    {
        try
        {
            button.IsEnabled = false;
            button.Content = "排队中...";

            var downloadId = await AppServices.Download.StartDownloadAsync(
                version.Url,
                version.FileName,
                software.Name,
                version.Version);

            button.Content = "下载中";
            UpdateStatus($"已开始下载: {software.Name} {version.Version}", false);
            UpdateActiveDownloadCount();

            AppServices.Download.ProgressChanged += OnProgressChanged;
            AppServices.Download.DownloadCompleted += OnDownloadCompleted;
            AppServices.Download.DownloadFailed += OnDownloadFailed;

            void OnProgressChanged(object? s, DownloadProgressEventArgs args)
            {
                if (args.DownloadId != downloadId) return;
                Dispatcher.UIThread.Post(() =>
                {
                    button.Content = $"{args.Progress}%";
                    UpdateStatus($"下载中: {software.Name} - {args.Progress}% ({FormatBytes((long)args.Speed)}/s)", false);
                });
            }

            void OnDownloadCompleted(object? s, string id)
            {
                if (id != downloadId) return;
                Cleanup();
                Dispatcher.UIThread.Post(() =>
                {
                    button.Content = "完成";
                    button.IsEnabled = true;
                    UpdateStatus($"下载完成: {software.Name} {version.Version}", false);
                    UpdateActiveDownloadCount();

                    var settings = AppServices.Config.Load();
                    var msg = settings.AutoExtractPortable
                        ? $"绿色版已下载并解压到应用目录\n{settings.AppsDir}\n\n是否打开应用目录？"
                        : "软件已下载完成\n\n是否打开下载目录？";
                    var openDir = settings.AutoExtractPortable ? settings.AppsDir : settings.CacheDir;
                    if (MessageBox.Show(msg, "下载完成", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        Process.Start("explorer.exe", openDir);
                });
            }

            void OnDownloadFailed(object? s, (string Id, string Error) args)
            {
                if (args.Id != downloadId) return;
                Cleanup();
                Dispatcher.UIThread.Post(() =>
                {
                    button.Content = "下载";
                    button.IsEnabled = true;
                    UpdateStatus($"下载失败: {args.Error}", true);
                    MessageBox.Show($"下载失败: {args.Error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }

            void Cleanup()
            {
                AppServices.Download.ProgressChanged -= OnProgressChanged;
                AppServices.Download.DownloadCompleted -= OnDownloadCompleted;
                AppServices.Download.DownloadFailed -= OnDownloadFailed;
            }
        }
        catch (Exception ex)
        {
            button.Content = "下载";
            button.IsEnabled = true;
            UpdateStatus($"下载失败: {ex.Message}", true);
            MessageBox.Show($"下载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnOpenDownloadManager_Click(object? sender, RoutedEventArgs e)
    {
        var manager = new DownloadManagerWindow();
        if (this is { } owner)
            manager.Show(owner);
        else
            manager.Show();
    }

    private void UpdateActiveDownloadCount()
    {
        var active = AppServices.DownloadHistory.GetActive().Count;
        if (active > 0)
            TxtCount.Text = $"共 {_filteredSoftware.Count} 项 | 活动下载 {active} 个";
    }

    private void UpdateStatus(string message, bool isError)
    {
        TxtStatus.Text = message;
        TxtStatus.Foreground = isError ? Brushes.Red : Brushes.Green;
    }

    private void UpdateCount()
    {
        UpdateActiveDownloadCount();
        if (TxtCount.Text?.Contains("活动下载") != true)
            TxtCount.Text = $"共 {_filteredSoftware.Count} 项";
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        var order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
