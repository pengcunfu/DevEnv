using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core;
using DevEnv.Core.DiskCleaner.Models;
using DevEnv.Core.LavaEnv.Services;
using DevEnv.Core.Services;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.ViewModels;

/// <summary>
/// C 盘清理：扫描内置的临时文件 / 系统缓存 / 浏览器缓存 / 回收站，
/// 展示各自占用大小，按勾选清理，完成后自动重新扫描。
/// </summary>
public sealed partial class DiskCleanerViewModel : ObservableObject, IDisposable
{
    private CancellationTokenSource? _cts;
    private bool _autoScanStarted;

    public DiskCleanerViewModel()
    {
        IsElevated = ElevationHelper.IsAdministrator();
        UpdateDriveSpace();
    }

    public ObservableCollection<CleanupTargetItem> Targets { get; } = [];

    public bool IsElevated { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanScan), nameof(CanClean), nameof(CanCancel))]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand), nameof(CleanCommand), nameof(CancelCommand),
        nameof(SelectAllCommand), nameof(ClearSelectionCommand))]
    private bool _isScanning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanScan), nameof(CanClean), nameof(CanCancel))]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand), nameof(CleanCommand), nameof(CancelCommand),
        nameof(SelectAllCommand), nameof(ClearSelectionCommand))]
    private bool _isCleaning;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _statusText = "点击「扫描 C 盘」开始检查";

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _driveSummary = "C 盘容量读取中…";

    [ObservableProperty]
    private string _driveHint = string.Empty;

    [ObservableProperty]
    private double _driveUsedPercent;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CleanCommand))]
    private int _selectedCount;

    [ObservableProperty]
    private string _selectedSummary = "尚未选择清理项";

    public bool CanScan => !IsScanning && !IsCleaning;

    public bool CanClean => CanScan && SelectedCount > 0;

    public bool CanCancel => IsScanning || IsCleaning;

    /// <summary>视图首次加载时自动扫描一次；之后由用户手动点扫描。</summary>
    public void AutoScan()
    {
        if (_autoScanStarted)
            return;
        _autoScanStarted = true;
        if (ScanCommand.CanExecute(null))
            ScanCommand.Execute(null);
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        var cts = new CancellationTokenSource();
        _cts = cts;
        IsScanning = true;
        ProgressPercent = 0;
        HasError = false;
        Message = string.Empty;
        StatusText = "正在扫描临时文件与缓存…";

        try
        {
            var token = cts.Token;
            var scan = await Task.Run(() =>
            {
                var definitions = CoreServices.DiskCleanup.GetTargets();
                return CoreServices.DiskCleanup.Scan(definitions, token);
            }, token);

            ApplyScan(scan);
            UpdateDriveSpace();
            StatusText = "扫描完成";
            Message = BuildScanMessage(scan);
            HasError = false;
        }
        catch (OperationCanceledException)
        {
            StatusText = "扫描已取消";
        }
        catch (Exception ex)
        {
            StatusText = "扫描失败";
            Message = $"扫描失败：{ex.Message}";
            HasError = true;
        }
        finally
        {
            if (ReferenceEquals(_cts, cts))
                _cts = null;
            cts.Dispose();
            IsScanning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanClean))]
    private async Task CleanAsync()
    {
        var selected = Targets.Where(t => t.IsSelected).ToList();
        if (selected.Count == 0)
        {
            Message = "请先勾选要清理的项目";
            HasError = true;
            return;
        }

        var totalBytes = selected.Sum(t => t.SizeBytes);
        var totalFiles = selected.Sum(t => t.FileCount);
        if (totalBytes <= 0 && totalFiles <= 0)
        {
            Message = "所选项目没有可清理的内容";
            HasError = true;
            return;
        }

        var includesRecycleBin = selected.Any(t => t.Definition.IsRecycleBin);
        var needsAdmin = !IsElevated && selected.Any(t => t.Definition.RequiresAdministrator);
        var warning =
            $"即将清理 {selected.Count} 项，预计释放 {FormatSize(totalBytes)}。\n\n"
            + (includesRecycleBin ? "C 盘回收站会被清空，删除后无法恢复。\n" : string.Empty)
            + "临时文件与缓存删除后无法恢复。"
            + (needsAdmin
                ? "\n\n其中包含需要管理员权限的系统目录，普通权限下会被跳过；可先点「以管理员身份重启」。"
                : string.Empty)
            + "\n\n确定继续吗？";

        if (!Windows.ConfirmDialog.Show(HostServices.DialogOwner, warning, "清理 C 盘"))
            return;

        var definitions = selected.Select(t => t.Definition).ToList();
        var cts = new CancellationTokenSource();
        _cts = cts;
        IsCleaning = true;
        ProgressPercent = 0;
        HasError = false;
        Message = string.Empty;
        StatusText = "正在清理…";

        var progress = new Progress<CleanupProgress>(p =>
        {
            // 任务结束后可能还有排队的进度回调，忽略，避免把最终状态刷回中间值。
            if (!IsCleaning)
                return;

            ProgressPercent = p.Percent;
            var name = string.IsNullOrEmpty(p.CurrentPath)
                ? string.Empty
                : Path.GetFileName(p.CurrentPath);
            StatusText = $"{p.TargetName} · {p.ProcessedFiles}/{p.TotalFiles} 个文件 · {p.Percent:0}%"
                + (name.Length > 0 ? " · " + name : string.Empty);
        });

        CleanupResult? result = null;
        try
        {
            var token = cts.Token;
            result = await Task.Run(
                () => CoreServices.DiskCleanup.Clean(
                    definitions, totalBytes, totalFiles, progress, token),
                token);
        }
        catch (OperationCanceledException)
        {
            StatusText = "清理已取消";
        }
        catch (Exception ex)
        {
            StatusText = "清理失败";
            Message = $"清理失败：{ex.Message}";
            HasError = true;
        }
        finally
        {
            if (ReferenceEquals(_cts, cts))
                _cts = null;
            cts.Dispose();
            IsCleaning = false;
        }

        if (result is null)
            return;

        ProgressPercent = result.Canceled ? ProgressPercent : 100;
        StatusText = result.Canceled ? "清理已取消" : "清理完成";

        // 未取消时重新扫描，让列表反映清理后的实际占用；取消后不再自动扫描，
        // 避免用户刚点完取消又立刻开始一轮耗时扫描。
        if (!result.Canceled)
            await ScanAsync();
        Message = BuildCleanMessage(result);
        HasError = !string.IsNullOrEmpty(result.Outcomes.FirstOrDefault(o => o.Error is not null)?.Error);
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        if (_cts is null)
            return;
        _cts.Cancel();
        StatusText = IsCleaning ? "正在取消清理…" : "正在取消扫描…";
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    private void SelectAll()
    {
        foreach (var target in Targets)
            target.IsSelected = true;
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    private void ClearSelection()
    {
        foreach (var target in Targets)
            target.IsSelected = false;
    }

    [RelayCommand]
    private void RestartElevated()
    {
        if (!Windows.ConfirmDialog.Show(
                HostServices.DialogOwner,
                "将以管理员身份重启燧岚开发盒子，当前打开的工具窗口会关闭。是否继续？",
                "重启"))
        {
            return;
        }

        if (HostCallbacks.RestartElevated is null)
        {
            Message = "当前环境不支持自动重启，请手动以管理员身份运行";
            HasError = true;
        }
        else
        {
            HostCallbacks.RestartElevated(true);
        }
    }

    private void ApplyScan(CleanupScanResult scan)
    {
        // 重新扫描时保留用户已经勾选 / 取消的状态；第一次扫描用默认勾选。
        var selection = Targets.ToDictionary(
            t => t.Definition.Id, t => t.IsSelected, StringComparer.OrdinalIgnoreCase);

        foreach (var target in Targets)
            target.PropertyChanged -= OnTargetPropertyChanged;
        Targets.Clear();

        foreach (var item in scan.Items)
        {
            var selected = selection.TryGetValue(item.Definition.Id, out var existing)
                ? existing
                : item.Definition.DefaultSelected;

            var entry = new CleanupTargetItem(item.Definition, IsElevated) { IsSelected = selected };
            entry.UpdateScan(item);
            entry.PropertyChanged += OnTargetPropertyChanged;
            Targets.Add(entry);
        }

        UpdateSelectionSummary();
    }

    private void OnTargetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CleanupTargetItem.IsSelected))
            UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        var selected = Targets.Where(t => t.IsSelected).ToList();
        SelectedCount = selected.Count;
        SelectedSummary = selected.Count == 0
            ? "尚未选择清理项"
            : $"已选 {selected.Count} 项，预计释放 {FormatSize(selected.Sum(t => t.SizeBytes))}";
    }

    private void UpdateDriveSpace()
    {
        var space = CoreServices.DiskCleanup.GetDriveSpace();
        if (!space.Available)
        {
            DriveSummary = "C 盘容量不可用";
            DriveHint = string.Empty;
            DriveUsedPercent = 0;
            return;
        }

        DriveSummary = $"可用 {FormatSize(space.FreeBytes)} / 共 {FormatSize(space.TotalBytes)}";
        DriveHint = $"已用 {space.UsedPercent:0.0}% · 勾选清理项后点「清理所选」";
        DriveUsedPercent = space.UsedPercent;
    }

    private static string BuildScanMessage(CleanupScanResult scan)
    {
        var message = $"扫描完成：可清理 {FormatSize(scan.TotalBytes)}，共 {scan.TotalFiles:N0} 个文件"
            + $"（{scan.Elapsed.TotalSeconds:0.0} 秒）";
        if (scan.Warnings.Count > 0)
            message += $"；{scan.Warnings.Count} 个位置无法完整读取";
        if (scan.TotalBytes <= 0 && scan.TotalFiles <= 0)
            message = "扫描完成：C 盘很干净，没有发现可清理的内容";
        return message;
    }

    private static string BuildCleanMessage(CleanupResult result)
    {
        var parts = new List<string>
        {
            $"已释放 {FormatSize(result.TotalFreedBytes)}",
            $"删除 {result.TotalDeletedFiles} 个文件",
        };

        var directories = result.Outcomes.Sum(o => o.DeletedDirectories);
        if (directories > 0)
            parts.Add($"清理 {directories} 个目录");
        if (result.TotalSkippedFiles > 0)
            parts.Add($"{result.TotalSkippedFiles} 个文件被占用或权限不足已跳过");
        if (result.Outcomes.Any(o => o.RecycleBinEmptied))
            parts.Add("回收站已清空");

        var text = (result.Canceled ? "已取消：" : string.Empty) + string.Join("，", parts);
        var error = result.Outcomes.FirstOrDefault(o => o.Error is not null)?.Error;
        if (!string.IsNullOrEmpty(error))
            text += $"；{error}";
        return text + "。";
    }

    private static string FormatSize(long bytes) => HashCalculatorService.FormatFileSize(Math.Max(0, bytes));

    public void Dispose()
    {
        try
        {
            // 只取消，不在这里 Dispose：后台任务可能仍持有 token，由任务自身的 finally 释放。
            _cts?.Cancel();
        }
        catch
        {
            // 窗口关闭路径，忽略
        }
    }
}

/// <summary>C 盘清理列表的一行。</summary>
public sealed partial class CleanupTargetItem : ObservableObject
{
    private readonly bool _elevated;

    public CleanupTargetItem(CleanupTargetDefinition definition, bool elevated)
    {
        Definition = definition;
        _elevated = elevated;
    }

    public CleanupTargetDefinition Definition { get; }

    public string Name => Definition.Name;

    public string Category => Definition.Category;

    public string Description => Definition.Description;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SizeText), nameof(StatusText))]
    private long _sizeBytes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileCountText), nameof(StatusText))]
    private int _fileCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SizeText), nameof(FileCountText), nameof(StatusText))]
    private bool _exists;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _accessible = true;

    [ObservableProperty]
    private bool _isSelected;

    public bool HasContent => SizeBytes > 0 || FileCount > 0;

    public string SizeText => Exists && HasContent ? HashCalculatorService.FormatFileSize(SizeBytes) : "—";

    public string FileCountText => Exists && HasContent ? FileCount.ToString("N0") : "—";

    public string PermissionText => Definition.RequiresAdministrator ? "需管理员" : "当前用户";

    public string StatusText
    {
        get
        {
            if (Definition.RequiresAdministrator && !_elevated)
                return "需要管理员";
            if (Definition.IsRecycleBin && !Exists)
                return "空";
            if (!Exists)
                return "未找到";
            if (!Accessible)
                return "部分无法访问";
            if (!HasContent)
                return "无需清理";
            return "可清理";
        }
    }

    public void UpdateScan(CleanupScanItem item)
    {
        SizeBytes = item.SizeBytes;
        FileCount = item.FileCount;
        Exists = item.Exists;
        Accessible = item.Accessible;
        OnPropertyChanged(nameof(HasContent));
    }
}
