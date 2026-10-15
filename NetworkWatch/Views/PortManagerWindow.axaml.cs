using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NetworkWatch.Helpers;
using NetworkWatch.Models;
using NetworkWatch.Services;
using NetworkWatch.UI;

namespace NetworkWatch.Views;

public partial class PortManagerWindow : Window
{
    private readonly PortManagerService _service = new();
    private readonly ObservableCollection<PortConnection> _allConnections = new();
    private readonly ObservableCollection<PortConnection> _filteredConnections = new();
    private readonly DispatcherTimer _timer;
    private string _filter = "";

    public PortManagerWindow()
    {
        InitializeComponent();
        ConnectionGrid.ItemsSource = _filteredConnections;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => LoadConnections();

        UpdateElevation();
        LoadConnections();
        Closed += (_, _) => _timer.Stop();
    }

    private void LoadConnections()
    {
        var connections = _service.GetConnections();
        _allConnections.Clear();
        foreach (var conn in connections)
            _allConnections.Add(conn);

        RefreshFilter();
        UpdateStatus();
    }

    private void RefreshFilter()
    {
        _filteredConnections.Clear();
        foreach (var conn in _allConnections)
        {
            if (MatchesFilter(conn))
                _filteredConnections.Add(conn);
        }
    }

    private void UpdateStatus()
    {
        var total = _allConnections.Count;
        var shown = _filteredConnections.Count;
        StatusText.Text = string.IsNullOrEmpty(_filter)
            ? $"共 {total} 条连接"
            : $"共 {total} 条连接（显示 {shown} 条）";
    }

    private void UpdateElevation() =>
        ElevationText.Text = AdminHelper.IsRunningAsAdministrator() ? "管理员模式" : "普通模式";

    private bool MatchesFilter(PortConnection conn)
    {
        if (string.IsNullOrWhiteSpace(_filter))
            return true;

        return conn.LocalPort.ToString().Contains(_filter, StringComparison.OrdinalIgnoreCase)
               || conn.RemotePort.ToString().Contains(_filter, StringComparison.OrdinalIgnoreCase)
               || conn.LocalAddress.Contains(_filter, StringComparison.OrdinalIgnoreCase)
               || conn.RemoteAddress.Contains(_filter, StringComparison.OrdinalIgnoreCase)
               || conn.ProcessId.ToString().Contains(_filter, StringComparison.Ordinal)
               || conn.ProcessName.Contains(_filter, StringComparison.OrdinalIgnoreCase);
    }

    private void FilterBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        _filter = FilterBox.Text?.Trim() ?? "";
        RefreshFilter();
        UpdateStatus();
    }

    private void RefreshButton_Click(object? sender, RoutedEventArgs e) => LoadConnections();

    private void AutoRefreshCheckBox_Changed(object? sender, RoutedEventArgs e)
    {
        if (AutoRefreshCheckBox.IsChecked == true)
            _timer.Start();
        else
            _timer.Stop();
    }

    private void ConnectionGrid_ContextMenuOpening(object? sender, CancelEventArgs e)
    {
        if (sender is not ContextMenu menu)
            return;

        var hasSelection = ConnectionGrid.SelectedItems.Count > 0;
        foreach (var item in menu.Items.OfType<MenuItem>())
            item.IsEnabled = hasSelection;
    }

    private void ConnectionGrid_DoubleTapped(object? sender, TappedEventArgs e) => ShowDetail();

    private void ViewDetailMenuItem_Click(object? sender, RoutedEventArgs e) => ShowDetail();

    private void ShowDetail()
    {
        if (ConnectionGrid.SelectedItems.OfType<PortConnection>().FirstOrDefault() is not { } conn)
            return;

        var detail = new ProcessDetailWindow(conn.ProcessId, conn);
        detail.Show(this);
    }

    private void KillMenuItem_Click(object? sender, RoutedEventArgs e) => _ = KillSelectedProcessesAsync();

    private async Task KillSelectedProcessesAsync()
    {
        var pids = ConnectionGrid.SelectedItems
            .OfType<PortConnection>()
            .Select(c => c.ProcessId)
            .Distinct()
            .ToList();

        if (pids.Count == 0)
            return;

        var killed = 0;
        var failures = new List<string>();
        foreach (var pid in pids)
        {
            try
            {
                using var proc = Process.GetProcessById(pid);
                proc.Kill();
                proc.WaitForExit(3000);
                killed++;
            }
            catch (ArgumentException)
            {
                failures.Add($"PID {pid}（进程不存在）");
            }
            catch (Win32Exception)
            {
                failures.Add($"PID {pid}（权限不足）");
            }
            catch (Exception ex)
            {
                failures.Add($"PID {pid}（{ex.Message}）");
            }
        }

        LoadConnections();

        if (failures.Count == 0)
        {
            if (killed > 0)
                await NwDialog.ShowAsync(this, $"已终止 {killed} 个进程", "端口管理");
            return;
        }

        var message = $"已终止 {killed} 个进程。\n以下进程终止失败:\n" + string.Join("\n", failures);
        await NwDialog.ShowAsync(this, message, "端口管理");
    }
}
