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
    private readonly DispatcherTimer _messageTimer;

    public ClipboardViewModel()
    {
        Entries = [];
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _timer.Tick += (_, _) => Reload();
        _timer.Start();

        // 状态栏的操作反馈显示几秒后自动消失，统计信息常驻。
        _messageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _messageTimer.Tick += (_, _) =>
        {
            _messageTimer.Stop();
            Message = string.Empty;
        };

        Reload();
    }

    public ObservableCollection<ClipboardEntryView> Entries { get; }

    public IReadOnlyList<(string Mode, string Label)> ClearModes { get; } =
    [
        ("all", "全部"),
        ("today", "今天"),
        ("yesterday", "昨天"),
        ("daybefore", "前天"),
    ];

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private string _typeFilter = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private ClipboardEntryView? _selected;

    /// <summary>右键菜单中的复制 / 删除是否可用。</summary>
    public bool HasSelection => Selected is not null;

    [ObservableProperty]
    private string _message = string.Empty;

    /// <summary>底部状态栏常驻的统计信息。</summary>
    [ObservableProperty]
    private string _statsText = string.Empty;

    partial void OnMessageChanged(string value)
    {
        _messageTimer.Stop();
        if (!string.IsNullOrEmpty(value))
            _messageTimer.Start();
    }

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
        RefreshStats();
    }

    private void RefreshStats()
    {
        var stats = CoreServices.Clipboard.GetStats();
        StatsText = $"共 {stats.Total} 条 · 文本 {stats.Text} · 图片 {stats.Image} · 仅保留最近 {stats.RetentionDays} 天";
    }

    [RelayCommand]
    public void Paste(ClipboardEntryView? entry)
    {
        var target = entry ?? Selected;
        if (target is null)
            return;
        var result = CoreServices.Clipboard.Paste(target.Id);
        Message = result.Ok ? "已复制到系统剪贴板" : result.DisplayMessage;
    }

    [RelayCommand]
    private void Delete(ClipboardEntryView? entry)
    {
        var target = entry ?? Selected;
        if (target is null)
            return;

        var result = CoreServices.Clipboard.Delete(target.Id);
        Message = result.Ok ? "已删除 1 条" : result.DisplayMessage;
        Selected = null;
        Reload();
    }

    /// <summary>按时间范围清空指定类型的历史。type 为 "text" / "image"，null 表示全部。</summary>
    public void Clear(string mode, string? type)
    {
        var label = ClearModes.FirstOrDefault(m => m.Mode == mode).Label ?? mode;
        var typeLabel = type switch
        {
            "text" => "文本",
            "image" => "图片",
            _ => "全部",
        };
        if (!Windows.ConfirmDialog.Show(HostServices.DialogOwner, $"清空「{label}」的{typeLabel}记录？", "清空"))
            return;

        var removed = CoreServices.Clipboard.Clear(mode, type);
        Message = removed > 0 ? $"已删除 {removed} 条" : "没有符合条件的记录";
        Selected = null;
        Reload();
    }

    [RelayCommand]
    private void Export()
    {
        var result = CoreServices.Clipboard.Export();
        Message = result.DisplayMessage;
    }

    public void Dispose()
    {
        _timer.Stop();
        _messageTimer.Stop();
    }
}