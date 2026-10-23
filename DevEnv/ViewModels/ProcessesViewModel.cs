using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core.Models;
using DevEnv.Core.Services;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.ViewModels;

/// <summary>进程监控页：实时 CPU / 内存、搜索排序、结束进程与定位目录。</summary>
public sealed partial class ProcessesViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _timer;

    public ProcessesViewModel()
    {
        Rows = [];
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => { if (!Paused) Refresh(); };
        _timer.Start();
        Refresh();
    }

    public ObservableCollection<ProcessRowView> Rows { get; }

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private string _sort = "cpu";

    [ObservableProperty]
    private bool _paused;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private ProcessRowView? _selected;

    public bool HasSelection => Selected is not null;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private int _processCount;

    [ObservableProperty]
    private string _totalMemoryText = "—";

    [ObservableProperty]
    private string _timestamp = "—";

    [ObservableProperty]
    private string _statusHint = string.Empty;

    partial void OnQueryChanged(string value) => ApplyFilter();

    partial void OnSortChanged(string value) => Refresh();

    public IEnumerable<string> SortModes { get; } = ["cpu", "memory", "name"];

    private IEnumerable<ProcessRowView> _all = [];

    [RelayCommand]
    public void Refresh()
    {
        var snapshot = CoreServices.Processes.GetSnapshot(Sort);
        _all = snapshot.Processes;
        ProcessCount = snapshot.ProcessCount;
        TotalMemoryText = snapshot.TotalMemoryText;
        Timestamp = snapshot.Timestamp.ToString("HH:mm:ss");
        StatusHint = snapshot.StatusHint ?? string.Empty;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = Query.Trim();
        var rows = string.IsNullOrEmpty(query)
            ? _all
            : _all.Where(p =>
                p.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || p.ProcessId.ToString().Contains(query, StringComparison.Ordinal)
                || p.PathText.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        var selectedId = Selected?.ProcessId;
        Rows.Clear();
        foreach (var row in rows)
            Rows.Add(row);
        Selected = selectedId is null ? null : Rows.FirstOrDefault(r => r.ProcessId == selectedId);
    }

    [RelayCommand]
    private void TogglePause() => Paused = !Paused;

    [RelayCommand]
    private void Kill()
    {
        if (Selected is null)
            return;
        if (!Windows.ConfirmDialog.Show(HostServices.DialogOwner,
                $"确定要结束进程「{Selected.DisplayName}」(PID {Selected.ProcessId}) 吗？", "结束"))
            return;

        var result = CoreServices.Processes.Kill(Selected.ProcessId, entireProcessTree: false);
        if (!result.Ok)
            Message = result.DisplayMessage;
        Selected = null;
        Refresh();
    }

    [RelayCommand]
    private void KillTree()
    {
        if (Selected is null)
            return;
        if (!Windows.ConfirmDialog.Show(HostServices.DialogOwner,
                $"确定要结束进程树「{Selected.DisplayName}」(PID {Selected.ProcessId}) 吗？", "结束"))
            return;

        var result = CoreServices.Processes.Kill(Selected.ProcessId, entireProcessTree: true);
        if (!result.Ok)
            Message = result.DisplayMessage;
        Selected = null;
        Refresh();
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (Selected is null)
            return;
        var result = CoreServices.Processes.OpenFolder(Selected.ProcessId);
        if (!result.Ok)
            Message = result.DisplayMessage;
    }

    public void Dispose() => _timer.Stop();
}