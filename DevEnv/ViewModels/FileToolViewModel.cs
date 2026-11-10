using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core;
using DevEnv.Core.FileTools.Models;
using DevEnv.Core.FileTools.Services;
using DevEnv.Core.LavaEnv.Services;
using DevEnv.Core.Services;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.ViewModels;

/// <summary>文件解锁与粉碎：Restart Manager 定位占用进程，随机数据多遍覆盖后删除。</summary>
public sealed partial class FileToolViewModel : ObservableObject, IDisposable
{
    private CancellationTokenSource? _shredCts;

    public FileToolViewModel()
    {
        ShredEntries.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CanStartShred));
            OnPropertyChanged(nameof(ShredSummary));
        };
    }

    #region 解除占用

    [ObservableProperty]
    private string _lockPath = string.Empty;

    [ObservableProperty]
    private string _lockMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanScan))]
    private bool _isScanning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLockSelection))]
    private LockedProcessInfo? _selectedLock;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLockProcesses))]
    private int _lockProcessCount;

    [ObservableProperty]
    private bool _isElevated = ElevationHelper.IsAdministrator();

    public ObservableCollection<LockedProcessInfo> LockProcesses { get; } = [];

    public bool CanScan => !IsScanning;

    public bool HasLockSelection => SelectedLock is not null;

    public bool HasLockProcesses => LockProcessCount > 0;

    [RelayCommand]
    private void PickLockFile()
    {
        var files = HostCallbacks.PickFiles?.Invoke("所有文件|*.*");
        if (files is { Length: > 0 })
            LockPath = files[0];
    }

    [RelayCommand]
    private void PickLockFolder()
    {
        var folder = HostCallbacks.PickFolder?.Invoke();
        if (!string.IsNullOrWhiteSpace(folder))
            LockPath = folder;
    }

    [RelayCommand]
    private async Task ScanLockAsync()
    {
        if (string.IsNullOrWhiteSpace(LockPath))
        {
            LockMessage = "请先选择文件或文件夹";
            return;
        }

        IsScanning = true;
        SelectedLock = null;
        LockProcesses.Clear();
        LockProcessCount = 0;
        LockMessage = "正在检测占用…";

        try
        {
            var path = LockPath;
            var (result, scan) = await Task.Run(() => CoreServices.FileUnlock.Scan(path));
            if (!result.Ok || scan is null)
            {
                LockMessage = result.DisplayMessage;
                return;
            }

            foreach (var process in scan.Processes)
                LockProcesses.Add(process);
            LockProcessCount = LockProcesses.Count;

            var message = LockProcessCount == 0
                ? "未发现占用该路径的进程"
                : $"发现 {LockProcessCount} 个进程正在占用";
            if (scan.Truncated)
                message += $"（文件夹内文件过多，仅检测前 {FileUnlockService.MaxFilesPerScan} 个）";
            if (scan.Warnings.Count > 0)
                message += $"；{scan.Warnings.Count} 条提示";
            if (!IsElevated)
                message += "。当前为普通权限，部分系统进程可能检测不到或无法结束";
            LockMessage = message;
        }
        catch (Exception ex)
        {
            LockMessage = $"检测失败：{ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task EndLockProcessAsync(LockedProcessInfo? process)
    {
        var target = process ?? SelectedLock;
        if (target is null)
            return;

        if (!target.CanEnd)
        {
            LockMessage = target.IsCritical
                ? "关键系统进程不允许结束"
                : "不能结束当前程序自身";
            return;
        }

        if (!Windows.ConfirmDialog.Show(
                HostServices.DialogOwner,
                $"确定要结束进程「{target.Name}」(PID {target.ProcessId}) 吗？\n未保存的数据可能丢失。",
                "结束进程"))
            return;

        var result = CoreServices.FileUnlock.EndProcess(target);
        await ScanLockAsync();
        if (!result.Ok)
            LockMessage = $"{result.DisplayMessage}；{LockMessage}";
        else
            LockMessage = $"已结束「{target.Name}」。{LockMessage}";
    }

    [RelayCommand]
    private async Task EndAllLockProcessesAsync()
    {
        var targets = LockProcesses.Where(p => p.CanEnd).ToList();
        if (targets.Count == 0)
        {
            LockMessage = "列表中没有可结束的进程";
            return;
        }

        if (!Windows.ConfirmDialog.Show(
                HostServices.DialogOwner,
                $"确定要结束列表中的 {targets.Count} 个进程吗？\n未保存的数据可能丢失。",
                "全部结束"))
            return;

        var succeeded = 0;
        var failures = new List<string>();
        foreach (var process in targets)
        {
            var result = CoreServices.FileUnlock.EndProcess(process);
            if (result.Ok)
                succeeded++;
            else
                failures.Add($"{process.Name}：{result.DisplayMessage}");
        }

        await ScanLockAsync();
        var summary = $"已结束 {succeeded}/{targets.Count} 个进程";
        if (failures.Count > 0)
            summary += $"；失败：{failures[0]}";
        LockMessage = $"{summary}。{LockMessage}";
    }

    [RelayCommand]
    private void RestartElevated()
    {
        if (!Windows.ConfirmDialog.Show(
                HostServices.DialogOwner,
                "将以管理员身份重启FireNeb DevEnv，当前打开的工具窗口会关闭。是否继续？",
                "重启"))
            return;

        if (HostCallbacks.RestartElevated is null)
            LockMessage = "当前环境不支持自动重启，请手动以管理员身份运行";
        else
            HostCallbacks.RestartElevated(true);
    }

    #endregion

    #region 文件粉碎

    [ObservableProperty]
    private string _shredInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasShredSelection))]
    private ShredEntry? _selectedShred;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartShred), nameof(CanEditShredList), nameof(CanCancelShred))]
    private bool _isShredding;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PassCount))]
    private int _passIndex = 1;

    [ObservableProperty]
    private double _shredPercent;

    [ObservableProperty]
    private string _shredStatus = string.Empty;

    [ObservableProperty]
    private string _shredMessage = string.Empty;

    [ObservableProperty]
    private bool _shredHasError;

    public ObservableCollection<ShredEntry> ShredEntries { get; } = [];

    public IReadOnlyList<string> PassOptions { get; } = ["1 遍（快速）", "3 遍（推荐）", "7 遍（严格）"];

    public int PassCount => PassIndex switch
    {
        0 => 1,
        2 => 7,
        _ => 3,
    };

    public bool HasShredSelection => SelectedShred is not null;

    public bool CanEditShredList => !IsShredding;

    public bool CanStartShred => !IsShredding && ShredEntries.Count > 0;

    public bool CanCancelShred => IsShredding;

    public string ShredSummary => ShredEntries.Count == 0
        ? "尚未添加文件"
        : $"已添加 {ShredEntries.Count} 项";

    [RelayCommand]
    private void PickShredFiles()
    {
        var files = HostCallbacks.PickFiles?.Invoke("所有文件|*.*");
        if (files is { Length: > 0 })
            AddShredPaths(files);
    }

    [RelayCommand]
    private void PickShredFolder()
    {
        var folder = HostCallbacks.PickFolder?.Invoke();
        if (!string.IsNullOrWhiteSpace(folder))
            AddShredPaths([folder]);
    }

    [RelayCommand]
    private void AddShredInput()
    {
        if (string.IsNullOrWhiteSpace(ShredInput))
            return;
        AddShredPaths([ShredInput]);
    }

    /// <summary>供视图拖放调用：去重、合并父子路径后加入粉碎列表。</summary>
    public void AddShredPaths(IEnumerable<string> paths)
    {
        if (IsShredding)
            return;

        var added = 0;
        var duplicate = 0;
        var invalid = 0;
        string? lastError = null;

        foreach (var raw in paths)
        {
            var validation = ShredPathPolicy.Validate(raw);
            if (validation is not null)
            {
                invalid++;
                lastError = validation;
                continue;
            }

            var entry = CreateEntry(raw);
            if (entry is null)
            {
                invalid++;
                continue;
            }

            // 已添加的父目录已覆盖该路径，或完全重复：跳过。
            if (ShredEntries.Any(existing =>
                    ShredPathPolicy.IsSameOrUnder(entry.Path, existing.Path)))
            {
                duplicate++;
                continue;
            }

            // 新加入的父目录覆盖列表中已有子项：移除子项。
            for (var i = ShredEntries.Count - 1; i >= 0; i--)
            {
                if (ShredPathPolicy.IsSameOrUnder(ShredEntries[i].Path, entry.Path))
                    ShredEntries.RemoveAt(i);
            }

            ShredEntries.Add(entry);
            added++;
        }

        if (added > 0)
            ShredInput = string.Empty;

        var parts = new List<string>();
        if (added > 0)
            parts.Add($"已添加 {added} 项");
        if (duplicate > 0)
            parts.Add($"{duplicate} 项已在列表中");
        if (invalid > 0)
            parts.Add(lastError is null ? $"{invalid} 项路径无效" : $"{invalid} 项无法添加：{lastError}");
        ShredMessage = parts.Count > 0 ? string.Join("；", parts) : ShredMessage;
        ShredHasError = invalid > 0 && added == 0;
    }

    [RelayCommand]
    private void RemoveShredEntry(ShredEntry? entry)
    {
        var target = entry ?? SelectedShred;
        if (target is null || IsShredding)
            return;
        ShredEntries.Remove(target);
        SelectedShred = null;
    }

    [RelayCommand]
    private void ClearShredEntries()
    {
        if (IsShredding)
            return;
        ShredEntries.Clear();
        SelectedShred = null;
        ShredMessage = string.Empty;
        ShredHasError = false;
        ShredPercent = 0;
        ShredStatus = string.Empty;
    }

    [RelayCommand]
    private async Task StartShredAsync()
    {
        if (IsShredding || ShredEntries.Count == 0)
            return;

        var entries = ShredEntries.ToList();
        var problems = new List<string>();
        foreach (var entry in entries)
        {
            var error = ShredPathPolicy.Validate(entry.Path);
            if (error is not null)
                problems.Add($"{entry.Path}：{error}");
        }

        if (problems.Count > 0)
        {
            ShredMessage = $"无法开始：{string.Join("；", problems.Take(3))}";
            ShredHasError = true;
            return;
        }

        var hasFolder = entries.Any(e => e.Kind == "文件夹");
        var warning =
            $"即将粉碎 {entries.Count} 项。\n\n" +
            $"将用随机数据覆盖文件 {PassCount} 遍后删除，操作不可恢复。" +
            (hasFolder ? "文件夹内的所有文件都会一并处理。" : string.Empty) +
            "\n\n确定继续吗？";

        if (!Windows.ConfirmDialog.Show(HostServices.DialogOwner, warning, "粉碎"))
            return;

        IsShredding = true;
        ShredPercent = 0;
        ShredStatus = "准备中…";
        ShredMessage = string.Empty;
        ShredHasError = false;
        _shredCts = new CancellationTokenSource();

        var progress = new Progress<ShredProgress>(p =>
        {
            // 任务结束后可能还有排队的进度回调，忽略，避免把「完成」刷回中间值。
            if (!IsShredding)
                return;

            ShredPercent = p.Percent;
            if (string.IsNullOrEmpty(p.CurrentPath) && p.Percent >= 100)
            {
                ShredStatus = "完成";
                return;
            }

            var name = string.IsNullOrEmpty(p.CurrentPath)
                ? string.Empty
                : Path.GetFileName(p.CurrentPath);
            ShredStatus = p.Pass > 0
                ? $"{p.FileIndex}/{p.FileCount} · 第 {p.Pass}/{p.Passes} 遍 · {p.Percent:0}% · {name}"
                : "准备中…";
        });

        try
        {
            var options = new ShredOptions
            {
                Passes = PassCount,
                DeleteAfterShred = true,
                RandomizeName = true,
            };
            var paths = entries.Select(e => e.Path).ToList();
            var token = _shredCts.Token;

            var result = await Task.Run(
                () => CoreServices.FileShred.ShredAsync(paths, options, progress, token));

            ShredPercent = result.Canceled ? ShredPercent : 100;
            ShredStatus = result.Canceled ? "已取消" : "完成";
            ShredMessage = BuildShredMessage(result);
            ShredHasError = !result.Ok && !result.Canceled;
        }
        catch (Exception ex)
        {
            ShredMessage = $"粉碎失败：{ex.Message}";
            ShredHasError = true;
        }
        finally
        {
            IsShredding = false;
            _shredCts?.Dispose();
            _shredCts = null;
            RefreshShredEntries();
        }
    }

    [RelayCommand]
    private void CancelShred()
    {
        if (_shredCts is null)
            return;
        _shredCts.Cancel();
        ShredStatus = "正在取消…";
    }

    private static string BuildShredMessage(ShredResult result)
    {
        if (result.FatalError is not null)
            return result.FatalError;

        var removedDirs = result.RemovedDirectories > 0
            ? $"，删除 {result.RemovedDirectories} 个目录"
            : string.Empty;
        var warningText = result.Warnings.Count > 0
            ? $"，{result.Warnings.Count} 条目录提示"
            : string.Empty;

        if (result.Canceled)
            return $"已取消：{result.ShreddedFiles} 个文件已粉碎{removedDirs}，其余保留。";

        if (result.Ok)
            return $"完成：粉碎 {result.ShreddedFiles} 个文件{removedDirs}{warningText}。";

        var first = result.Failures.FirstOrDefault();
        var detail = first is null
            ? string.Empty
            : $"；首个失败：{Path.GetFileName(first.Path)}（{first.Error}）";
        return $"完成 {result.ShreddedFiles}/{result.TotalFiles} 个文件{removedDirs}，"
            + $"{result.Failures.Count} 个失败{detail}{warningText}";
    }

    private void RefreshShredEntries()
    {
        foreach (var entry in ShredEntries.ToList())
        {
            if (!File.Exists(entry.Path) && !Directory.Exists(entry.Path))
                ShredEntries.Remove(entry);
        }

        SelectedShred = null;
    }

    private static ShredEntry? CreateEntry(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(raw.Trim().Trim('"')));
            if (File.Exists(full))
            {
                return new ShredEntry
                {
                    Path = full,
                    Kind = "文件",
                    SizeText = HashCalculatorService.FormatFileSize(new FileInfo(full).Length),
                };
            }

            if (Directory.Exists(full))
            {
                return new ShredEntry
                {
                    Path = full,
                    Kind = "文件夹",
                    SizeText = "—",
                };
            }
        }
        catch
        {
            // 无效路径按「路径无效」处理
        }

        return null;
    }

    #endregion

    public void Dispose()
    {
        try
        {
            // 只取消，不在这里 Dispose：后台任务可能仍持有 token，
            // 由 StartShredAsync 的 finally 在任务结束后统一释放。
            _shredCts?.Cancel();
        }
        catch
        {
            // 窗口关闭路径，忽略
        }
    }

    /// <summary>粉碎列表的一行。</summary>
    public sealed class ShredEntry
    {
        public string Path { get; init; } = string.Empty;

        public string Name => System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(Path));

        public string Kind { get; init; } = "文件";

        public string SizeText { get; init; } = string.Empty;
    }
}
