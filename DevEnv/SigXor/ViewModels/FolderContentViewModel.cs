using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core.SigXor;

namespace DevEnv.Host.SigXor.ViewModels;

/// <summary>
/// 文件夹预览里的文件管理器：目录导航、文件列表与打开文件。
/// 双击 / 回车文件夹进入下级目录，双击 / 回车文件交给外层预览窗打开。
/// </summary>
public sealed partial class FolderContentViewModel : ObservableObject
{
    private readonly Func<FileEntry, Task> _openFile;
    private readonly string _startPath;
    private readonly Stack<string> _backStack = new();
    private readonly Stack<string> _forwardStack = new();
    private CancellationTokenSource? _navCts;
    private bool _disposed;

    public FolderContentViewModel(string path, Func<FileEntry, Task> openFile)
    {
        _startPath = path;
        _openFile = openFile;
    }

    public ObservableCollection<FileEntry> Entries { get; } = [];

    [ObservableProperty]
    private string _currentDirectory = "";

    [ObservableProperty]
    private FileEntry? _selectedEntry;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _canGoForward;

    public Task InitializeAsync() => NavigateToAsync(_startPath, addHistory: false);

    public async Task NavigateToAsync(string path, bool addHistory = true)
    {
        if (_disposed)
            return;

        if (!Directory.Exists(path))
        {
            if (FileSystemService.GetParent(path) is not { } parent)
                return;
            path = parent;
        }

        if (addHistory
            && CurrentDirectory.Length > 0
            && !string.Equals(CurrentDirectory, path, StringComparison.OrdinalIgnoreCase))
        {
            _backStack.Push(CurrentDirectory);
            _forwardStack.Clear();
        }

        _navCts?.Cancel();
        _navCts?.Dispose();
        var cts = new CancellationTokenSource();
        _navCts = cts;

        CurrentDirectory = path;
        StatusText = "正在加载…";
        try
        {
            var list = await Task.Run(() => FileSystemService.Enumerate(path), cts.Token);
            if (cts.IsCancellationRequested || _disposed)
                return;

            Entries.Clear();
            foreach (var entry in list)
                Entries.Add(entry);
            SelectedEntry = Entries.FirstOrDefault();
            StatusText = $"{Entries.Count} 项";
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败：{ex.Message}";
        }

        UpdateNavigationState();
    }

    [RelayCommand]
    private async Task NavigateUpAsync()
    {
        if (FileSystemService.GetParent(CurrentDirectory) is { } parent)
            await NavigateToAsync(parent);
    }

    [RelayCommand]
    private async Task NavigateBackAsync()
    {
        if (_backStack.Count == 0)
            return;

        _forwardStack.Push(CurrentDirectory);
        await NavigateToAsync(_backStack.Pop(), addHistory: false);
    }

    [RelayCommand]
    private async Task NavigateForwardAsync()
    {
        if (_forwardStack.Count == 0)
            return;

        _backStack.Push(CurrentDirectory);
        await NavigateToAsync(_forwardStack.Pop(), addHistory: false);
    }

    [RelayCommand]
    private Task RefreshAsync() => NavigateToAsync(CurrentDirectory, addHistory: false);

    /// <summary>双击 / 回车：文件夹进入下级目录，文件交给外层预览窗打开。</summary>
    public async Task ActivateSelectedAsync()
    {
        if (SelectedEntry is not { } entry)
            return;

        if (entry.IsDirectory)
            await NavigateToAsync(entry.FullPath);
        else
            await _openFile(entry);
    }

    public void Dispose()
    {
        _disposed = true;
        try { _navCts?.Cancel(); } catch { /* 预览关闭时忽略 */ }
        _navCts?.Dispose();
        _navCts = null;
    }

    private void UpdateNavigationState()
    {
        CanGoBack = _backStack.Count > 0;
        CanGoForward = _forwardStack.Count > 0;
    }
}
