using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NetworkWatch.Helpers;
using NetworkWatch.Models;
using NetworkWatch.Services;
using NetworkWatch.UI;

namespace NetworkWatch.Views;

public partial class NetworkWatchView : UserControl
{
    private NetworkMonitorService? _monitor;
    private readonly ObservableCollection<ProcessNetworkInfo> _allProcesses = new();
    private readonly ObservableCollection<ProcessNetworkInfo> _filteredProcesses = new();
    private MonitorSnapshot? _latestSnapshot;
    private bool _paused;
    private int? _selectedPid;
    private SpeedTestWindow? _speedTestWindow;
    private PortManagerWindow? _portManagerWindow;

    public NetworkWatchView()
    {
        InitializeComponent();
        ProcessGrid.ItemsSource = _filteredProcesses;
        ThemeService.LoadSaved();
        UpdateThemeButton();

        AttachedToVisualTree += (_, _) => StartMonitor();
        DetachedFromLogicalTree += (_, _) => StopMonitor();
    }

    private Window? OwnerWindow => TopLevel.GetTopLevel(this) as Window;

    private void StartMonitor()
    {
        if (_monitor is not null)
            return;

        _monitor = new NetworkMonitorService
        {
            SortByTraffic = AutoSortCheckBox.IsChecked == true,
        };
        _monitor.SnapshotUpdated += OnSnapshotUpdated;
        _monitor.Start();
    }

    private void StopMonitor()
    {
        if (_monitor is null)
            return;

        _monitor.SnapshotUpdated -= OnSnapshotUpdated;
        _monitor.Dispose();
        _monitor = null;
        _speedTestWindow?.Close();
        _portManagerWindow?.Close();
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e) =>
        RefreshFilter();

    private void RefreshFilter()
    {
        var query = SearchBox.Text;
        _filteredProcesses.Clear();
        foreach (var proc in _allProcesses)
        {
            if (MatchesFilter(proc, query))
                _filteredProcesses.Add(proc);
        }
    }

    private static bool MatchesFilter(ProcessNetworkInfo proc, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return proc.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
               || proc.ProcessId.ToString().Contains(query, StringComparison.Ordinal)
               || (proc.ExecutablePath?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private void OnSnapshotUpdated(MonitorSnapshot snapshot)
    {
        if (_paused)
            return;

        Dispatcher.UIThread.Post(() => ApplySnapshot(snapshot));
    }

    private void ApplySnapshot(MonitorSnapshot snapshot)
    {
        _latestSnapshot = snapshot;
        AdminButton.Content = snapshot.IsElevated ? "普通模式" : "管理员模式";
        ToolTip.SetTip(AdminButton, snapshot.IsElevated
            ? "当前以管理员权限运行；点击可恢复普通权限"
            : "点击以管理员身份重启，临时启用完整 ETW 流量统计");

        var selectedPid = _selectedPid;
        if (selectedPid is null && ProcessGrid.SelectedItem is ProcessNetworkInfo selected)
            selectedPid = selected.ProcessId;

        UpdateProcessList(snapshot.Processes);
        RefreshFilter();

        if (selectedPid is int pid)
        {
            var match = _filteredProcesses.FirstOrDefault(p => p.ProcessId == pid);
            if (match is not null)
            {
                ProcessGrid.SelectedItem = match;
                ShowConnections(match);
            }
        }

        var status =
            $"活跃进程: {snapshot.Processes.Count}  |  连接总数: {snapshot.TotalConnections}  |  流量采样: {snapshot.TrafficStatsSuccessCount}/{snapshot.TrafficStatsEligibleCount}  |  更新于 {snapshot.Timestamp:HH:mm:ss}";
        if (!string.IsNullOrEmpty(snapshot.StatusHint))
            status += $"  |  {snapshot.StatusHint}";
        StatusText.Text = status;
        TotalDownloadText.Text = TrafficFormatter.FormatRate(snapshot.TotalDownloadRate);
        TotalUploadText.Text = TrafficFormatter.FormatRate(snapshot.TotalUploadRate);
    }

    private void UpdateProcessList(IReadOnlyList<ProcessNetworkInfo> processes)
    {
        if (_monitor?.SortByTraffic != false)
        {
            _allProcesses.Clear();
            foreach (var proc in processes)
                _allProcesses.Add(proc);
            return;
        }

        var byPid = processes.ToDictionary(p => p.ProcessId);
        for (var i = _allProcesses.Count - 1; i >= 0; i--)
        {
            if (!byPid.ContainsKey(_allProcesses[i].ProcessId))
                _allProcesses.RemoveAt(i);
        }

        for (var i = 0; i < _allProcesses.Count; i++)
        {
            var pid = _allProcesses[i].ProcessId;
            if (byPid.TryGetValue(pid, out var updated))
                _allProcesses[i] = updated;
        }

        var existing = new HashSet<int>(_allProcesses.Select(p => p.ProcessId));
        foreach (var proc in processes)
        {
            if (!existing.Contains(proc.ProcessId))
                _allProcesses.Add(proc);
        }
    }

    private void AutoSortCheckBox_Changed(object? sender, RoutedEventArgs e)
    {
        if (_monitor is not null)
            _monitor.SortByTraffic = AutoSortCheckBox.IsChecked == true;
        if (_latestSnapshot is not null)
            ApplySnapshot(_latestSnapshot);
    }

    private void ProcessGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ProcessGrid.SelectedItem is ProcessNetworkInfo proc)
        {
            _selectedPid = proc.ProcessId;
            ShowConnections(proc);
        }
    }

    private void ShowConnections(ProcessNetworkInfo proc)
    {
        SelectedProcessText.Text = string.IsNullOrWhiteSpace(proc.ExecutablePath)
            ? $"{proc.DisplayName}  (PID {proc.ProcessId})"
            : $"{proc.DisplayName}  (PID {proc.ProcessId})\n{proc.ExecutablePath}";

        ConnectionGrid.ItemsSource = proc.Connections;
    }

    private void PauseButton_Click(object? sender, RoutedEventArgs e)
    {
        _paused = !_paused;
        PauseButton.Content = _paused ? "继续" : "暂停";
        if (!_paused && _latestSnapshot is not null)
            ApplySnapshot(_latestSnapshot);
    }

    private async void RefreshButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_monitor is null)
            return;

        try
        {
            var snapshot = _monitor.CollectSnapshot();
            ApplySnapshot(snapshot);
        }
        catch (Exception ex)
        {
            await NwDialog.ShowAsync(OwnerWindow, $"刷新失败: {ex.Message}", "网络监控");
        }
    }

    private void ThemeButton_Click(object? sender, RoutedEventArgs e)
    {
        ThemeService.Toggle();
        UpdateThemeButton();
    }

    private void UpdateThemeButton() =>
        ThemeButton.Content = ThemeService.Current == AppTheme.Dark ? "深色" : "浅色";

    private void SpeedTestButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_speedTestWindow is { IsVisible: true })
        {
            _speedTestWindow.Activate();
            return;
        }

        _speedTestWindow = new SpeedTestWindow();
        if (OwnerWindow is { } owner)
            _speedTestWindow.Show(owner);
        else
            _speedTestWindow.Show();
    }

    private void PortManagerButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_portManagerWindow is { IsVisible: true })
        {
            _portManagerWindow.Activate();
            return;
        }

        _portManagerWindow = new PortManagerWindow();
        if (OwnerWindow is { } owner)
            _portManagerWindow.Show(owner);
        else
            _portManagerWindow.Show();
    }

    private void AdminButton_Click(object? sender, RoutedEventArgs e)
    {
        if (AdminHelper.IsRunningAsAdministrator())
            RestartAsNormalUser();
        else
            RestartAsAdministrator();
    }

    private async void RestartAsAdministrator()
    {
        var (fileName, arguments) = GetRestartCommand();
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Environment.CurrentDirectory,
            });
            ShutdownApp();
        }
        catch (Win32Exception)
        {
            // 用户取消了 UAC
        }
        catch (Exception ex)
        {
            await NwDialog.ShowAsync(OwnerWindow, $"无法以管理员身份启动: {ex.Message}", "网络监控");
        }
    }

    private async void RestartAsNormalUser()
    {
        var (fileName, arguments) = GetRestartCommand();
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{fileName}\" {arguments}".TrimEnd(),
                UseShellExecute = true,
            });
            ShutdownApp();
        }
        catch (Exception ex)
        {
            await NwDialog.ShowAsync(OwnerWindow, $"无法以普通权限重启: {ex.Message}", "网络监控");
        }
    }

    private static void ShutdownApp()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
        else
            Environment.Exit(0);
    }

    private static (string FileName, string Arguments) GetRestartCommand()
    {
        var processPath = Environment.ProcessPath;
        var assemblyPath = Assembly.GetEntryAssembly()?.Location;
        if (processPath is not null &&
            Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(assemblyPath))
            return (processPath, $"\"{assemblyPath}\"");

        return (processPath ?? assemblyPath ?? string.Empty, string.Empty);
    }
}
