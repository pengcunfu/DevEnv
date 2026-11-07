using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core.Models;
using DevEnv.Core.ServiceWatch.Models;
using DevEnv.Core.Services;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.ViewModels;

/// <summary>服务监控页：枚举 Windows 服务，支持启停、重启、暂停、修改启动类型与查看依赖。</summary>
public sealed partial class ServicesViewModel : ObservableObject, IDisposable
{
    public sealed record FilterOption(string Id, string Title);
    public sealed record SortOption(string Id, string Title);
    public sealed record StartupOption(ServiceStartupMode Mode, string Title);

    private readonly DispatcherTimer _timer;
    private int _refreshing;
    private bool _disposed;
    private string? _startupSourceName;
    private string? _lastSignature;
    private IEnumerable<ServiceInfo> _all = [];

    public ServicesViewModel()
    {
        Rows = [];

        StatusFilters =
        [
            new FilterOption("all", "全部状态"),
            new FilterOption("running", "运行中"),
            new FilterOption("stopped", "已停止"),
        ];
        SortOptions =
        [
            new SortOption("display", "按显示名"),
            new SortOption("name", "按服务名"),
            new SortOption("status", "按状态"),
        ];
        StartupOptions =
        [
            new StartupOption(ServiceStartupMode.Automatic, "自动"),
            new StartupOption(ServiceStartupMode.AutomaticDelayed, "自动（延迟启动）"),
            new StartupOption(ServiceStartupMode.Manual, "手动"),
            new StartupOption(ServiceStartupMode.Disabled, "禁用"),
        ];

        _selectedStatusFilter = StatusFilters[0];
        _selectedSortOption = SortOptions[0];
        _selectedStartupOption = StartupOptions[0];

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += (_, _) => { if (!Paused && !IsActing) _ = Refresh(); };
        _timer.Start();

        _ = Refresh();
    }

    public ObservableCollection<ServiceInfo> Rows { get; }

    public IReadOnlyList<FilterOption> StatusFilters { get; }
    public IReadOnlyList<SortOption> SortOptions { get; }
    public IReadOnlyList<StartupOption> StartupOptions { get; }

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private FilterOption _selectedStatusFilter;

    [ObservableProperty]
    private SortOption _selectedSortOption;

    [ObservableProperty]
    private StartupOption? _selectedStartupOption;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RefreshToggleText))]
    private bool _paused;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(CanStartSelected))]
    [NotifyPropertyChangedFor(nameof(CanStopSelected))]
    [NotifyPropertyChangedFor(nameof(CanRestartSelected))]
    [NotifyPropertyChangedFor(nameof(CanPauseSelected))]
    [NotifyPropertyChangedFor(nameof(CanContinueSelected))]
    [NotifyPropertyChangedFor(nameof(CanOpenFolderSelected))]
    [NotifyPropertyChangedFor(nameof(CanApplyStartupType))]
    private ServiceInfo? _selected;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private int _serviceCount;

    [ObservableProperty]
    private int _runningCount;

    [ObservableProperty]
    private int _stoppedCount;

    [ObservableProperty]
    private string _timestamp = "—";

    [ObservableProperty]
    private string _statusHint = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ElevationText))]
    [NotifyPropertyChangedFor(nameof(ElevationActionText))]
    private bool _isElevated;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartSelected))]
    [NotifyPropertyChangedFor(nameof(CanStopSelected))]
    [NotifyPropertyChangedFor(nameof(CanRestartSelected))]
    [NotifyPropertyChangedFor(nameof(CanPauseSelected))]
    [NotifyPropertyChangedFor(nameof(CanContinueSelected))]
    [NotifyPropertyChangedFor(nameof(CanOpenFolderSelected))]
    [NotifyPropertyChangedFor(nameof(CanApplyStartupType))]
    private bool _isActing;

    public bool HasSelection => Selected is not null;
    public bool CanStartSelected => !IsActing && Selected is { IsStopped: true };
    public bool CanStopSelected => !IsActing && Selected is { CanStop: true, IsRunningOrPaused: true };
    public bool CanRestartSelected => !IsActing && Selected is { CanStop: true, IsRunning: true };
    public bool CanPauseSelected => !IsActing && Selected is { CanPauseAndContinue: true, IsRunning: true };
    public bool CanContinueSelected => !IsActing && Selected is { CanPauseAndContinue: true, IsPaused: true };
    public bool CanOpenFolderSelected => Selected is not null && !string.IsNullOrWhiteSpace(Selected.ExecutablePath);
    public bool CanApplyStartupType => !IsActing && Selected is not null && SelectedStartupOption is not null;

    public string ElevationText => IsElevated ? "管理员模式" : "普通权限";
    public string ElevationActionText => IsElevated ? "降权重启" : "管理员重启";
    public string RefreshToggleText => Paused ? "继续刷新" : "暂停刷新";

    partial void OnQueryChanged(string value) => ApplyFilter();
    partial void OnSelectedStatusFilterChanged(FilterOption value) => ApplyFilter();
    partial void OnSelectedSortOptionChanged(SortOption value) => ApplyFilter();

    partial void OnSelectedChanged(ServiceInfo? value)
    {
        if (value is null)
        {
            _startupSourceName = null;
            return;
        }

        if (value.ServiceName == _startupSourceName)
            return;

        SyncStartupOption(value);
    }

    private void SyncStartupOption(ServiceInfo? value)
    {
        if (value is null)
        {
            _startupSourceName = null;
            return;
        }

        _startupSourceName = value.ServiceName;
        var mode = ServiceText.ToStartupMode(value.StartType, value.IsDelayedAutoStart);
        SelectedStartupOption = StartupOptions.FirstOrDefault(option => option.Mode == mode) ?? StartupOptions[0];
    }

    /// <summary>采样放到线程池，避免 SCM / 注册表枚举阻塞界面。</summary>
    [RelayCommand]
    public async Task Refresh()
    {
        if (Interlocked.Exchange(ref _refreshing, 1) == 1)
            return;

        try
        {
            var snapshot = await Task.Run(() => CoreServices.WindowsServices.GetSnapshot());
            if (_disposed)
                return;

            _all = snapshot.Services;
            ServiceCount = snapshot.TotalCount;
            RunningCount = snapshot.RunningCount;
            StoppedCount = snapshot.StoppedCount;
            IsElevated = snapshot.IsElevated;
            Timestamp = snapshot.Timestamp.ToString("HH:mm:ss");
            StatusHint = snapshot.StatusHint ?? string.Empty;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            if (!_disposed)
                Message = $"读取服务列表失败：{ex.Message}";
        }
        finally
        {
            Interlocked.Exchange(ref _refreshing, 0);
        }
    }

    private void ApplyFilter()
    {
        IEnumerable<ServiceInfo> rows = _all;

        switch (SelectedStatusFilter.Id)
        {
            case "running":
                rows = rows.Where(service => service.IsRunning);
                break;
            case "stopped":
                rows = rows.Where(service => service.IsStopped);
                break;
        }

        var query = Query.Trim();
        if (query.Length > 0)
        {
            rows = rows.Where(service =>
                Contains(service.DisplayName, query)
                || Contains(service.ServiceName, query)
                || Contains(service.Description, query)
                || Contains(service.LogOnAs, query)
                || Contains(service.ExecutablePath, query));
        }

        rows = SelectedSortOption.Id switch
        {
            "name" => rows.OrderBy(service => service.ServiceName, StringComparer.OrdinalIgnoreCase),
            "status" => rows
                .OrderBy(service => service.StatusRank)
                .ThenBy(service => service.DisplayName, StringComparer.CurrentCultureIgnoreCase),
            _ => rows.OrderBy(service => service.DisplayName, StringComparer.CurrentCultureIgnoreCase),
        };

        var visible = rows.ToList();
        var signature = BuildSignature(visible);
        if (string.Equals(signature, _lastSignature, StringComparison.Ordinal))
            return;

        // 服务状态大多不变：数据没变化时不要清空列表，避免每 3 秒把滚动位置与选中项冲掉。
        var selectedName = Selected?.ServiceName;
        Rows.Clear();
        foreach (var row in visible)
            Rows.Add(row);

        _lastSignature = signature;
        Selected = selectedName is null
            ? null
            : Rows.FirstOrDefault(row => string.Equals(row.ServiceName, selectedName, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildSignature(IReadOnlyList<ServiceInfo> rows)
    {
        var builder = new StringBuilder(rows.Count * 48);
        foreach (var row in rows)
        {
            builder.Append(row.ServiceName).Append('|')
                .Append(row.DisplayName).Append('|')
                .Append(row.Status).Append('|')
                .Append(row.StartType).Append('|')
                .Append(row.IsDelayedAutoStart ? '1' : '0').Append('|')
                .Append(row.CanStop ? '1' : '0')
                .Append(row.CanPauseAndContinue ? '1' : '0').Append('|')
                .Append(row.Description).Append('|')
                .Append(row.LogOnAs).Append('|')
                .Append(row.ImagePath).Append('|')
                .Append(row.DependenciesText).Append('|')
                .Append(row.DependentsText).Append('\n');
        }

        return builder.ToString();
    }

    private static bool Contains(string? text, string query) =>
        !string.IsNullOrEmpty(text) && text.Contains(query, StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private void TogglePause() => Paused = !Paused;

    [RelayCommand]
    private Task StartService()
    {
        if (Selected is not { } service)
            return Task.CompletedTask;

        return RunActionAsync(() => CoreServices.WindowsServices.Start(service.ServiceName));
    }

    [RelayCommand]
    private Task StopService()
    {
        if (Selected is not { } service)
            return Task.CompletedTask;
        if (!Windows.ConfirmDialog.Show(HostServices.DialogOwner,
                $"确定要停止服务「{service.DisplayName}」吗？依赖它的服务可能无法正常工作。", "停止"))
            return Task.CompletedTask;

        return RunActionAsync(() => CoreServices.WindowsServices.Stop(service.ServiceName));
    }

    [RelayCommand]
    private Task RestartService()
    {
        if (Selected is not { } service)
            return Task.CompletedTask;
        if (!Windows.ConfirmDialog.Show(HostServices.DialogOwner,
                $"确定要重启服务「{service.DisplayName}」吗？", "重启"))
            return Task.CompletedTask;

        return RunActionAsync(() => CoreServices.WindowsServices.Restart(service.ServiceName));
    }

    [RelayCommand]
    private Task PauseService()
    {
        if (Selected is not { } service)
            return Task.CompletedTask;

        return RunActionAsync(() => CoreServices.WindowsServices.Pause(service.ServiceName));
    }

    [RelayCommand]
    private Task ContinueService()
    {
        if (Selected is not { } service)
            return Task.CompletedTask;

        return RunActionAsync(() => CoreServices.WindowsServices.Continue(service.ServiceName));
    }

    [RelayCommand]
    private Task ApplyStartupType()
    {
        if (Selected is not { } service || SelectedStartupOption is not { } option)
            return Task.CompletedTask;

        var current = ServiceText.DescribeStartType(service.StartType, service.IsDelayedAutoStart);
        var target = ServiceText.DescribeStartupMode(option.Mode);
        if (string.Equals(current, target, StringComparison.Ordinal))
        {
            HostServices.SetStatus($"「{service.DisplayName}」的启动类型已经是{target}", "services");
            return Task.CompletedTask;
        }

        if (!Windows.ConfirmDialog.Show(HostServices.DialogOwner,
                $"将服务「{service.DisplayName}」的启动类型由「{current}」改为「{target}」？", "修改"))
            return Task.CompletedTask;

        return RunActionAsync(() =>
            CoreServices.WindowsServices.SetStartupType(service.ServiceName, option.Mode));
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (Selected is null)
            return;

        if (string.IsNullOrWhiteSpace(Selected.ExecutablePath))
        {
            Message = "无法解析该服务的可执行文件路径。";
            return;
        }

        HostServices.OpenFolder(Selected.ExecutablePath);
    }

    [RelayCommand]
    private void RestartElevated()
    {
        var asAdmin = !IsElevated;
        var prompt = asAdmin ? "将以管理员身份重启燧岚开发盒子？" : "将以降权普通用户重启？";
        if (!Windows.ConfirmDialog.Show(HostServices.DialogOwner, prompt, "重启"))
            return;

        var result = CoreServices.WindowsServices.RestartElevated(asAdmin);
        if (!result.Ok)
            Message = result.DisplayMessage;
    }

    private async Task RunActionAsync(Func<OperationResult> action)
    {
        if (IsActing)
            return;

        IsActing = true;
        Message = string.Empty;
        try
        {
            var result = await Task.Run(action);
            if (result.Ok)
            {
                HostServices.SetStatus(result.DisplayMessage, "services");
            }
            else
            {
                Message = result.DisplayMessage;
                HostServices.SetStatus(result.DisplayMessage, "services");
            }
        }
        catch (Exception ex)
        {
            Message = ex.Message;
            HostServices.SetStatus(ex.Message, "services");
        }
        finally
        {
            IsActing = false;
            await Refresh();
            // 操作可能失败或状态没变（列表不重建），这里强制让启动类型下拉框跟随真实状态
            SyncStartupOption(Selected);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
    }
}
