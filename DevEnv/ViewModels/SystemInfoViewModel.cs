using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core;
using DevEnv.Core.SystemInfo.Models;
using DevEnv.Core.SystemInfo.Services;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.ViewModels;

/// <summary>系统信息：后台采集硬件与系统快照，支持刷新与复制。</summary>
public sealed partial class SystemInfoViewModel : ObservableObject
{
    private const string PageId = "sysinfo";
    private readonly SystemInfoService _service = new();
    private bool _loaded;

    public ObservableCollection<SystemInfoGroup> Groups { get; } = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _status = "打开后自动读取系统信息";

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    /// <summary>视图首次显示时调用一次，避免每次切回窗口都重新查询 WMI。</summary>
    public async Task LoadOnceAsync()
    {
        if (_loaded)
            return;

        _loaded = true;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        _loaded = true;
        await LoadAsync();
    }

    [RelayCommand]
    private void CopyAll()
    {
        if (Groups.Count == 0)
        {
            HostServices.SetStatus("暂无可复制的信息，请先刷新", PageId);
            return;
        }

        var lines = new List<string>();
        foreach (var group in Groups)
        {
            lines.Add($"[{group.Title}]");
            lines.AddRange(group.Items.Select(item => $"  {item.Name}: {item.Value}"));
            lines.Add(string.Empty);
        }

        HostCallbacks.SetClipboardText?.Invoke(string.Join(Environment.NewLine, lines));
        Message = "已复制全部信息到剪贴板";
        HasError = false;
        HostServices.SetStatus(Message, PageId);
    }

    private async Task LoadAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        Message = string.Empty;
        HasError = false;
        Status = "正在读取系统信息…";

        try
        {
            var groups = await Task.Run(_service.Collect);

            Groups.Clear();
            foreach (var group in groups)
                Groups.Add(group);

            Status = $"已读取 {groups.Count} 组信息 · {DateTime.Now:HH:mm:ss}";
            HostServices.SetStatus($"系统信息：{groups.Sum(group => group.Items.Count)} 项", PageId);
        }
        catch (Exception ex)
        {
            Status = "读取失败";
            Message = ex.Message;
            HasError = true;
            HostServices.SetStatus($"读取系统信息失败：{ex.Message}", PageId);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
