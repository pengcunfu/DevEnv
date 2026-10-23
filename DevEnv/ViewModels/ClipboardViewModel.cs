using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core.Models;
using DevEnv.Core.Services;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.ViewModels;

/// <summary>熔岩剪贴板：历史列表 + 详情 + 还原 / 删除 / 清空 / 导出。</summary>
public sealed partial class ClipboardViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _timer;

    public ClipboardViewModel()
    {
        Entries = [];
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _timer.Tick += (_, _) => Reload();
        _timer.Start();
        Reload();
    }

    public ObservableCollection<ClipboardEntryView> Entries { get; }

    public IReadOnlyList<(string Mode, string Label)> ClearModes { get; } =
    [
        ("all", "全部"),
        ("today", "今天"),
        ("week", "本周"),
        ("month", "本月"),
    ];

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private string _typeFilter = string.Empty;

    [ObservableProperty]
    private ClipboardEntryView? _selected;

    [ObservableProperty]
    private string _message = string.Empty;

    partial void OnSearchChanged(string value) => Reload();

    partial void OnTypeFilterChanged(string value) => Reload();

    [RelayCommand]
    public void Reload()
    {
        var selectedId = Selected?.Id;
        Entries.Clear();
        foreach (var entry in CoreServices.Clipboard.GetEntries(Search, TypeFilter))
            Entries.Add(entry);
        Selected = selectedId is null ? null : Entries.FirstOrDefault(e => e.Id == selectedId);
    }

    [RelayCommand]
    public void Paste(ClipboardEntryView? entry)
    {
        var target = entry ?? Selected;
        if (target is null)
            return;
        var result = CoreServices.Clipboard.Paste(target.Id);
        Message = result.Ok ? "已写入系统剪贴板" : result.DisplayMessage;
    }

    [RelayCommand]
    private void Delete(ClipboardEntryView? entry)
    {
        var target = entry ?? Selected;
        if (target is null)
            return;

        var result = CoreServices.Clipboard.Delete(target.Id);
        if (!result.Ok)
            Message = result.DisplayMessage;
        Selected = null;
        Reload();
    }

    [RelayCommand]
    public void Clear(string mode)
    {
        var label = ClearModes.FirstOrDefault(m => m.Mode == mode).Label ?? mode;
        if (!Windows.ConfirmDialog.Show(HostServices.DialogOwner, $"清空「{label}」历史？", "清空"))
            return;

        var removed = CoreServices.Clipboard.Clear(mode);
        Message = $"已删除 {removed} 条";
        Selected = null;
        Reload();
    }

    [RelayCommand]
    private void Export()
    {
        var result = CoreServices.Clipboard.Export();
        Message = result.Ok ? result.DisplayMessage : result.DisplayMessage;
    }

    public void Dispose() => _timer.Stop();
}