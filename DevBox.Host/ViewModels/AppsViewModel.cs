using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBox.Core;
using DevBox.Core.Models;
using DevBox.Core.Services;
using DevBox.Host.Infrastructure;

namespace DevBox.Host.ViewModels;

/// <summary>本地应用页：卡片列表 + 右键菜单 + 拖入 exe 添加。</summary>
public sealed partial class AppsViewModel : ShortcutListViewModel
{
    public AppsViewModel()
    {
        Items = [];
        Reload();
    }

    public ObservableCollection<NativeAppItem> Items { get; }

    [ObservableProperty]
    private NativeAppItem? _selected;

    public string EmptyHint { get; } = "右键空白区域选择程序，或直接拖入 .exe";

    public override void Reload()
    {
        var items = CoreServices.Shortcuts.GetApps();
        Items.Clear();
        foreach (var item in items)
            Items.Add(item);
    }

    public override void OnCardActivated(object item, int clickCount)
    {
        if (item is NativeAppItem app && ShouldLaunch(clickCount))
            Run(app);
    }

    [RelayCommand]
    public void Add()
    {
        var path = HostCallbacks.PickExecutable?.Invoke();
        if (string.IsNullOrWhiteSpace(path))
            return;
        Report(CoreServices.Shortcuts.AddApp(path));
        Reload();
    }

    /// <summary>主窗拖放入口：把拖进来的 exe 加进来，返回是否成功。</summary>
    public bool AddFromDrop(string path)
    {
        var result = CoreServices.Shortcuts.AddApp(path);
        Report(result);
        if (result.Ok)
            Reload();
        return result.Ok;
    }

    [RelayCommand]
    public void Run(NativeAppItem? app)
    {
        var index = IndexOf(app);
        if (index >= 0)
            Report(CoreServices.Shortcuts.RunApp(index));
    }

    [RelayCommand]
    public void OpenFolder(NativeAppItem? app)
    {
        var index = IndexOf(app);
        if (index >= 0)
            Report(CoreServices.Shortcuts.OpenAppFolder(index));
    }

    [RelayCommand]
    public void Rename(NativeAppItem? app)
    {
        if (app is null)
            return;
        var index = IndexOf(app);
        if (index < 0)
            return;
        var name = Windows.PromptDialog.Show(HostServices.DialogOwner, "显示名称", app.Name);
        if (name is null)
            return;
        Report(CoreServices.Shortcuts.UpdateApp(index, name));
        Reload();
    }

    [RelayCommand]
    public void RefreshIcon(NativeAppItem? app)
    {
        if (app is null)
            return;
        var index = IndexOf(app);
        if (index < 0)
            return;
        Report(CoreServices.Shortcuts.UpdateApp(index, refreshIcon: true));
        Reload();
    }

    [RelayCommand]
    public void Remove(NativeAppItem? app)
    {
        if (app is null)
            return;
        if (!Windows.ConfirmDialog.Show(HostServices.DialogOwner, $"移除「{app.Name}」？", "移除"))
            return;
        var index = IndexOf(app);
        if (index < 0)
            return;
        Report(CoreServices.Shortcuts.RemoveApp(index));
        Reload();
    }

    private int IndexOf(NativeAppItem? app)
    {
        if (app is null)
            return -1;
        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i] == app)
                return i;
        }
        return -1;
    }
}