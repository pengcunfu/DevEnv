using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ProcessWatch.Models;
using ProcessWatch.Services;
using ProcessWatch.UI;

namespace ProcessWatch.Views;

public partial class ProcessWatchView : UserControl
{
    private ProcessMonitorService? _monitor;
    private readonly ObservableCollection<ProcessInfo> _allProcesses = new();
    private readonly ObservableCollection<ProcessInfo> _filteredProcesses = new();
    private ProcessSnapshot? _latestSnapshot;
    private bool _paused;
    private int? _selectedPid;

    public ProcessWatchView()
    {
        InitializeComponent();
        ProcessGrid.ItemsSource = _filteredProcesses;

        AttachedToVisualTree += (_, _) => StartMonitor();
        DetachedFromLogicalTree += (_, _) => StopMonitor();
    }

    private Window? OwnerWindow => TopLevel.GetTopLevel(this) as Window;

    private void StartMonitor()
    {
        if (_monitor is not null)
            return;

        _monitor = new ProcessMonitorService
        {
            SortMode = ReadSortMode(),
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
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e) =>
        RefreshFilter();

    private void SortComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_monitor is null)
            return;

        _monitor.SortMode = ReadSortMode();
        if (_latestSnapshot is not null)
            ApplySnapshot(ResortSnapshot(_latestSnapshot));
    }

    private ProcessSortMode ReadSortMode()
    {
        if (SortComboBox.SelectedItem is ComboBoxItem { Tag: string tag })
        {
            return tag switch
            {
                "Memory" => ProcessSortMode.Memory,
                "Name" => ProcessSortMode.Name,
                _ => ProcessSortMode.Cpu,
            };
        }

        return ProcessSortMode.Cpu;
    }

    private ProcessSnapshot ResortSnapshot(ProcessSnapshot snapshot)
    {
        IEnumerable<ProcessInfo> ordered = ReadSortMode() switch
        {
            ProcessSortMode.Memory => snapshot.Processes.OrderByDescending(p => p.WorkingSetBytes),
            ProcessSortMode.Name => snapshot.Processes.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase),
            _ => snapshot.Processes.OrderByDescending(p => p.CpuPercent).ThenByDescending(p => p.WorkingSetBytes),
        };

        return new ProcessSnapshot
        {
            Processes = ordered.ToList(),
            Timestamp = snapshot.Timestamp,
            ProcessCount = snapshot.ProcessCount,
            TotalWorkingSetBytes = snapshot.TotalWorkingSetBytes,
            StatusHint = snapshot.StatusHint,
        };
    }

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

    private static bool MatchesFilter(ProcessInfo proc, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return proc.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
               || proc.ProcessId.ToString().Contains(query, StringComparison.Ordinal)
               || (proc.ExecutablePath?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private void OnSnapshotUpdated(ProcessSnapshot snapshot)
    {
        if (_paused)
            return;

        Dispatcher.UIThread.Post(() => ApplySnapshot(snapshot));
    }

    private void ApplySnapshot(ProcessSnapshot snapshot)
    {
        _latestSnapshot = snapshot;

        var selectedPid = _selectedPid;
        if (selectedPid is null && ProcessGrid.SelectedItem is ProcessInfo selected)
            selectedPid = selected.ProcessId;

        _allProcesses.Clear();
        foreach (var proc in snapshot.Processes)
            _allProcesses.Add(proc);

        RefreshFilter();

        if (selectedPid is int pid)
        {
            var match = _filteredProcesses.FirstOrDefault(p => p.ProcessId == pid);
            if (match is not null)
            {
                ProcessGrid.SelectedItem = match;
                ShowDetails(match);
            }
            else
            {
                ClearDetails();
                UpdateActionButtons(null);
            }
        }

        var status = $"进程数: {snapshot.ProcessCount}";
        if (!string.IsNullOrEmpty(snapshot.StatusHint))
            status += $"  |  {snapshot.StatusHint}";
        StatusText.Text = status;
        TotalMemoryText.Text = snapshot.TotalMemoryText;
        UpdateTimeText.Text = $"更新于 {snapshot.Timestamp:HH:mm:ss}";
    }

    private void ProcessGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ProcessGrid.SelectedItem is ProcessInfo proc)
        {
            _selectedPid = proc.ProcessId;
            ShowDetails(proc);
            UpdateActionButtons(proc);
        }
        else
        {
            ClearDetails();
            UpdateActionButtons(null);
        }
    }

    private void ShowDetails(ProcessInfo proc)
    {
        DetailNameText.Text = proc.DisplayName;
        DetailPidText.Text = $"PID {proc.ProcessId}";
        DetailPathText.Text = string.IsNullOrWhiteSpace(proc.ExecutablePath)
            ? "路径不可用"
            : proc.ExecutablePath;
        DetailCpuText.Text = $"CPU: {proc.CpuText}";
        DetailMemoryText.Text = $"工作集: {proc.MemoryText}";
        DetailPrivateText.Text = $"专用内存: {proc.PrivateMemoryText}";
        DetailVirtualText.Text = $"虚拟内存: {proc.VirtualMemoryText}";
        DetailThreadsText.Text = $"线程: {proc.ThreadCount}";
        DetailHandlesText.Text = $"句柄: {proc.HandleCount}";
        DetailStartText.Text = $"启动时间: {proc.StartTimeText}";
        DetailRespondingText.Text = $"状态: {proc.RespondingText}";
    }

    private void ClearDetails()
    {
        DetailNameText.Text = "未选择进程";
        DetailPidText.Text = "";
        DetailPathText.Text = "";
        DetailCpuText.Text = "";
        DetailMemoryText.Text = "";
        DetailPrivateText.Text = "";
        DetailVirtualText.Text = "";
        DetailThreadsText.Text = "";
        DetailHandlesText.Text = "";
        DetailStartText.Text = "";
        DetailRespondingText.Text = "";
    }

    private void UpdateActionButtons(ProcessInfo? proc)
    {
        var hasSelection = proc is not null;
        KillButton.IsEnabled = hasSelection;
        KillTreeButton.IsEnabled = hasSelection;
        OpenFolderButton.IsEnabled = hasSelection
            && !string.IsNullOrWhiteSpace(proc!.ExecutablePath)
            && File.Exists(proc.ExecutablePath);
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
            var snapshot = await Task.Run(() => _monitor.CollectSnapshot());
            ApplySnapshot(snapshot);
        }
        catch (Exception ex)
        {
            await PwDialog.ShowAsync(OwnerWindow, $"刷新失败: {ex.Message}", "进程监控");
        }
    }

    private async void KillButton_Click(object? sender, RoutedEventArgs e)
        => await KillSelectedAsync(entireTree: false);

    private async void KillTreeButton_Click(object? sender, RoutedEventArgs e)
        => await KillSelectedAsync(entireTree: true);

    private async Task KillSelectedAsync(bool entireTree)
    {
        if (ProcessGrid.SelectedItem is not ProcessInfo proc)
            return;

        var action = entireTree ? "结束进程树" : "结束进程";
        var confirmed = await PwDialog.ConfirmAsync(
            OwnerWindow,
            $"确定要{action}「{proc.DisplayName}」(PID {proc.ProcessId}) 吗？",
            "进程监控");
        if (!confirmed)
            return;

        if (!ProcessActions.TryKill(proc.ProcessId, entireTree, out var error))
        {
            await PwDialog.ShowAsync(OwnerWindow, error ?? "结束失败", "进程监控");
            return;
        }

        _selectedPid = null;
        ClearDetails();
        UpdateActionButtons(null);

        if (_monitor is not null)
        {
            try
            {
                var snapshot = await Task.Run(() => _monitor.CollectSnapshot());
                ApplySnapshot(snapshot);
            }
            catch
            {
                // 忽略刷新失败
            }
        }
    }

    private async void OpenFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ProcessGrid.SelectedItem is not ProcessInfo proc)
            return;

        if (!ProcessActions.TryOpenFolder(proc.ExecutablePath, out var error))
            await PwDialog.ShowAsync(OwnerWindow, error ?? "无法打开目录", "进程监控");
    }
}
