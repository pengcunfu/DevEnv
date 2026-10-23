using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core;
using DevEnv.Core.Models;
using DevEnv.Core.Services;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.ViewModels;

/// <summary>网站页：卡片列表 + 右键菜单；添加时自动抓标题与 favicon。</summary>
public sealed partial class WebsViewModel : ShortcutListViewModel
{
    public WebsViewModel()
    {
        Items = [];
        Reload();
    }

    public ObservableCollection<WebItem> Items { get; }

    [ObservableProperty]
    private WebItem? _selected;

    public string EmptyHint { get; } = "右键空白区域添加网站";

    public override void Reload()
    {
        var items = CoreServices.Shortcuts.GetWebs();
        Items.Clear();
        foreach (var item in items)
            Items.Add(item);
    }

    public override void OnCardActivated(object item, int clickCount)
    {
        if (item is WebItem web && ShouldLaunch(clickCount))
            Open(web);
    }

    [RelayCommand]
    public async Task AddAsync()
    {
        var url = Windows.PromptDialog.Show(HostServices.DialogOwner, "输入网址");
        if (string.IsNullOrWhiteSpace(url))
            return;
        var title = Windows.PromptDialog.Show(HostServices.DialogOwner, "显示名称（可留空，自动抓取）");
        if (title is null)
            return;

        Message = "正在抓取标题与图标…";
        var result = await CoreServices.Shortcuts.AddWebAsync(url, string.IsNullOrWhiteSpace(title) ? null : title);
        Report(result);
        if (!result.Ok)
            Message = result.DisplayMessage;
        Reload();
    }

    [RelayCommand]
    public void Open(WebItem? web)
    {
        var index = IndexOf(web);
        if (index >= 0)
            Report(CoreServices.Shortcuts.OpenWeb(index));
    }

    [RelayCommand]
    public async Task RenameAsync(WebItem? web)
    {
        if (web is null)
            return;
        var index = IndexOf(web);
        if (index < 0)
            return;
        var name = Windows.PromptDialog.Show(HostServices.DialogOwner, "显示名称", web.Name);
        if (name is null)
            return;
        Report(await CoreServices.Shortcuts.UpdateWebAsync(index, name));
        Reload();
    }

    [RelayCommand]
    public async Task RefreshIconAsync(WebItem? web)
    {
        if (web is null)
            return;
        var index = IndexOf(web);
        if (index < 0)
            return;
        Report(await CoreServices.Shortcuts.UpdateWebAsync(index, refreshIcon: true));
        Reload();
    }

    [RelayCommand]
    public void Remove(WebItem? web)
    {
        if (web is null)
            return;
        if (!Windows.ConfirmDialog.Show(HostServices.DialogOwner, $"移除「{web.Name}」？", "移除"))
            return;
        var index = IndexOf(web);
        if (index < 0)
            return;
        Report(CoreServices.Shortcuts.RemoveWeb(index));
        Reload();
    }

    private int IndexOf(WebItem? web)
    {
        if (web is null)
            return -1;
        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i] == web)
                return i;
        }
        return -1;
    }
}