using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core.SigXor;
using DevEnv.Host.SigXor.Services;

namespace DevEnv.Host.SigXor.ViewModels;

/// <summary>
/// 空格预览浮窗的视图模型。窗口打开期间保留浏览历史，
/// 可以用 ← / →（或标题栏按钮）在上一个 / 下一个预览过的文件之间切换。
/// </summary>
public sealed partial class ShellPreviewViewModel : ObservableObject
{
    private readonly PreviewViewModel _preview;
    private readonly PreviewHistory _history = new();

    public ShellPreviewViewModel(SettingsService settings, BackupService backup)
    {
        _preview = new PreviewViewModel(settings, backup);
    }

    public PreviewViewModel Preview => _preview;

    public bool CanGoPrevious => _history.CanGoPrevious;

    public bool CanGoNext => _history.CanGoNext;

    public string PositionText => _history.PositionText;

    /// <summary>重新打开预览窗口时重置历史，当前项为这批文件的第一个。</summary>
    public void ResetFiles(IReadOnlyList<FileEntry> files)
    {
        _history.Reset(files);
        NotifyHistoryChanged();
        _ = ShowCurrentAsync();
    }

    /// <summary>窗口已打开时又预览了新文件：追加到历史并切到新文件。</summary>
    public void AppendFiles(IReadOnlyList<FileEntry> files)
    {
        _history.Append(files);
        NotifyHistoryChanged();
        _ = ShowCurrentAsync();
    }

    [RelayCommand]
    private void Previous()
    {
        if (!_history.MovePrevious()) return;
        NotifyHistoryChanged();
        _ = ShowCurrentAsync();
    }

    [RelayCommand]
    private void Next()
    {
        if (!_history.MoveNext()) return;
        NotifyHistoryChanged();
        _ = ShowCurrentAsync();
    }

    private async Task ShowCurrentAsync()
    {
        if (_history.Current is not { } file) return;
        await _preview.LoadAsync(file);
    }

    private void NotifyHistoryChanged()
    {
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
    }

    public void DisposeContent()
    {
        _preview.CloseDocument();
        _preview.IsOpen = false;
    }
}
