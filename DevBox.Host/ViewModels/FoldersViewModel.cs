using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBox.Core;
using DevBox.Core.Models;
using DevBox.Core.Services;
using DevBox.Host.Infrastructure;

namespace DevBox.Host.ViewModels;

/// <summary>文件夹页：卡片列表 + 右键菜单 + 拖入文件夹添加。</summary>
public sealed partial class FoldersViewModel : ShortcutListViewModel
{
    public FoldersViewModel()
    {
        Items = [];
        Reload();
    }

    public ObservableCollection<FolderItem> Items { get; }

    [ObservableProperty]
    private FolderItem? _selected;

    public string EmptyHint { get; } = "右键空白区域选择文件夹，或直接拖入文件夹";

    public override void Reload()
    {
        var items = CoreServices.Shortcuts.GetFolders();
        Items.Clear();
        foreach (var item in items)
            Items.Add(item);
    }

    public override void OnCardActivated(object item, int clickCount)
    {
        if (item is FolderItem folder && ShouldLaunch(clickCount))
            Open(folder);
    }

    [RelayCommand]
    public void Add()
    {
        var path = HostCallbacks.PickFolder?.Invoke();
        if (string.IsNullOrWhiteSpace(path))
            return;
        Report(CoreServices.Shortcuts.AddFolder(path));
        Reload();
    }

    /// <summary>主窗拖放入口：把拖进来的文件夹加进来，返回是否成功。</summary>
    public bool AddFromDrop(string path)
    {
        var result = CoreServices.Shortcuts.AddFolder(path);
        Report(result);
        if (result.Ok)
            Reload();
        return result.Ok;
    }

    [RelayCommand]
    public void Open(FolderItem? folder)
    {
        var index = IndexOf(folder);
        if (index >= 0)
            Report(CoreServices.Shortcuts.OpenFolder(index));
    }

    [RelayCommand]
    public void Rename(FolderItem? folder)
    {
        if (folder is null)
            return;
        var index = IndexOf(folder);
        if (index < 0)
            return;
        var name = Windows.PromptDialog.Show(HostServices.DialogOwner, "显示名称", folder.Name);
        if (name is null)
            return;
        Report(CoreServices.Shortcuts.UpdateFolder(index, name));
        Reload();
    }

    [RelayCommand]
    public void Remove(FolderItem? folder)
    {
        if (folder is null)
            return;
        if (!Windows.ConfirmDialog.Show(HostServices.DialogOwner, $"移除「{folder.Name}」？", "移除"))
            return;
        var index = IndexOf(folder);
        if (index < 0)
            return;
        Report(CoreServices.Shortcuts.RemoveFolder(index));
        Reload();
    }

    private int IndexOf(FolderItem? folder)
    {
        if (folder is null)
            return -1;
        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i] == folder)
                return i;
        }
        return -1;
    }
}