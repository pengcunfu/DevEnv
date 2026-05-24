using DevEnv.UI;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using DevEnv.Models;
using DevEnv.Services;

namespace DevEnv.Views
{
    public partial class DownloadManagerWindow : Window
    {
        private readonly ObservableCollection<DownloadRecord> _activeDownloads = new();
        private readonly ObservableCollection<DownloadRecord> _historyDownloads = new();
        private readonly DispatcherTimer _refreshTimer;

        public DownloadManagerWindow()
        {
            InitializeComponent();
            ActiveGrid.ItemsSource = _activeDownloads;
            HistoryGrid.ItemsSource = _historyDownloads;

            AppServices.DownloadHistory.HistoryChanged += (_, _) => Dispatcher.UIThread.Post(RefreshData);
            AppServices.Download.ProgressChanged += (_, _) => Dispatcher.UIThread.Post(RefreshData);

            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _refreshTimer.Tick += (_, _) => RefreshData();
            _refreshTimer.Start();

            RefreshData();
        }

        private void RefreshData()
        {
            var all = AppServices.DownloadHistory.GetAll();
            _activeDownloads.Clear();
            foreach (var item in all.Where(r => r.Status is DownloadStatus.Pending or DownloadStatus.Downloading))
                _activeDownloads.Add(item);

            _historyDownloads.Clear();
            foreach (var item in all)
                _historyDownloads.Add(item);

            var stats = AppServices.DownloadHistory.GetStatistics();
            TxtStatsTotal.Text = $"总下载任务: {stats.Total}";
            TxtStatsCompleted.Text = $"已完成: {stats.Completed}";
            TxtStatsFailed.Text = $"失败: {stats.Failed}";
            TxtStatsDownloading.Text = $"下载中: {stats.Downloading}";
            TxtStatsBytes.Text = $"已下载总量: {FormatBytes(stats.TotalBytes)}";
            TxtStatus.Text = $"活动下载 {stats.Downloading} 个，历史记录 {stats.Total} 条";
        }

        private void BtnRefresh_Click(object? sender, RoutedEventArgs e) => RefreshData();

        private void BtnClearCompleted_Click(object? sender, RoutedEventArgs e)
        {
            AppServices.DownloadHistory.ClearCompleted();
            RefreshData();
        }

        private void BtnClearFailed_Click(object? sender, RoutedEventArgs e)
        {
            AppServices.DownloadHistory.ClearFailed();
            RefreshData();
        }

        private void BtnClearAll_Click(object? sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("确定要清空所有下载历史吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                AppServices.DownloadHistory.ClearAll();
                RefreshData();
            }
        }

        private void BtnOpenFolder_Click(object? sender, RoutedEventArgs e)
        {
            var dir = AppServices.Config.Load().CacheDir;
            Directory.CreateDirectory(dir);
            Process.Start("explorer.exe", dir);
        }

        private void BtnCancel_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string id)
            {
                AppServices.Download.CancelDownload(id);
                RefreshData();
            }
        }

        private void BtnOpenFile_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string path)
            {
                if (File.Exists(path))
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
                else
                    MessageBox.Show("文件不存在", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _refreshTimer.Stop();
            base.OnClosed(e);
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
}

