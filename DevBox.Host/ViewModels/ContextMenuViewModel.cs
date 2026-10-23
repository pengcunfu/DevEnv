using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBox.Core;
using DevBox.Core.Models;
using DevBox.Core.Services;
using DevBox.Host.Infrastructure;

namespace DevBox.Host.ViewModels;

/// <summary>
/// 右键菜单管理：
/// 上半部分切换 Win11 一级 / 完整二级菜单，下半部分管理「用 XXX 打开」右键菜单项。
/// 数据与独立的 MenuManager 工具共用（文档\FNSoftware\MenuManager\menu_configs.json）。
/// </summary>
public sealed partial class ContextMenuViewModel : ObservableObject
{
    public ContextMenuViewModel()
    {
        Entries = [];
        Reload();
    }

    public ObservableCollection<ContextMenuItemViewModel> Entries { get; }

    #region Win11 菜单形态

    [ObservableProperty]
    private bool _isWindows11;

    [ObservableProperty]
    private bool _isClassicMenu;

    [ObservableProperty]
    private string _win11Status = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [RelayCommand]
    public void Reload()
    {
        var service = CoreServices.ContextMenu;

        IsWindows11 = service.IsWindows11;
        IsClassicMenu = service.IsWin11MenuDisabled;
        Win11Status = service.Win11StatusText;

        Entries.Clear();
        var index = 0;
        foreach (var entry in service.GetEntries())
        {
            Entries.Add(new ContextMenuItemViewModel(entry, index, service.IsEntryEnabled(entry)));
            index++;
        }
    }

    [RelayCommand]
    private void UseClassicMenu() => ApplyWin11Menu(useClassic: true);

    [RelayCommand]
    private void UseDefaultMenu() => ApplyWin11Menu(useClassic: false);

    private void ApplyWin11Menu(bool useClassic)
    {
        var result = CoreServices.ContextMenu.SetWin11ClassicMenu(useClassic);
        Message = result.DisplayMessage;
        IsWindows11 = CoreServices.ContextMenu.IsWindows11;
        IsClassicMenu = CoreServices.ContextMenu.IsWin11MenuDisabled;
        Win11Status = CoreServices.ContextMenu.Win11StatusText;
    }

    #endregion

    #region 自定义菜单项

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing))]
    private ContextMenuItemViewModel? _editing;

    public bool IsEditing => Editing is not null;

    [RelayCommand]
    private void NewEntry()
    {
        Editing = new ContextMenuItemViewModel(new ContextMenuEntry(), -1, false);
    }

    [RelayCommand]
    private void Edit(ContextMenuItemViewModel? item)
    {
        if (item is not null)
            Editing = item;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        Editing = null;
        OnPropertyChanged(nameof(IsEditing));
    }

    [RelayCommand]
    private void BrowseProgram()
    {
        if (Editing is null)
            return;
        var path = HostCallbacks.PickExecutable?.Invoke();
        if (string.IsNullOrWhiteSpace(path))
            return;
        Editing.Entry.Path = path;
        if (string.IsNullOrWhiteSpace(Editing.Entry.Name))
            Editing.Entry.Name = System.IO.Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(Editing.Entry.Root))
            Editing.Entry.Root = BuildRootKey(Editing.Entry.Name);
    }

    [RelayCommand]
    private void Save()
    {
        if (Editing is null)
            return;

        var service = CoreServices.ContextMenu;
        var index = Editing.Index;
        var entry = Editing.Entry;

        var previous = index >= 0 ? service.GetEntries().ElementAtOrDefault(index) : null;

        // root 改了就先把旧键删掉，避免留下孤儿菜单项
        if (previous is not null
            && !string.Equals(previous.Root, entry.Root, StringComparison.OrdinalIgnoreCase))
        {
            _ = service.RemoveEntryFromSystem(previous);
        }

        var saved = service.SaveEntry(entry, index >= 0 ? index : null);
        if (!saved.Ok)
        {
            Message = saved.DisplayMessage;
            return;
        }

        var applied = service.ApplyEntry(entry);
        Message = applied.Ok ? $"{entry.Name}：{applied.DisplayMessage}" : applied.DisplayMessage;

        Editing = null;
        OnPropertyChanged(nameof(IsEditing));
        Reload();
    }

    [RelayCommand]
    private void Delete(ContextMenuItemViewModel? item)
    {
        if (item is null)
            return;
        if (!Windows.ConfirmDialog.Show(HostServices.DialogOwner, $"删除「{item.Entry.Name}」并从注册表移除？", "删除"))
            return;

        var service = CoreServices.ContextMenu;
        var removed = service.RemoveEntryFromSystem(item.Entry);
        var deleted = service.DeleteEntry(item.Index);

        Message = deleted.Ok
            ? (removed.Ok ? $"{item.Entry.Name}：已删除" : $"配置已删除，注册表移除失败：{removed.DisplayMessage}")
            : deleted.DisplayMessage;

        Reload();
    }

    /// <summary>把菜单项同步到注册表（已存在则移除后重写）。</summary>
    [RelayCommand]
    private void Sync(ContextMenuItemViewModel? item)
    {
        if (item is null)
            return;
        var result = CoreServices.ContextMenu.ApplyEntry(item.Entry);
        Message = result.DisplayMessage;
        Reload();
    }

    /// <summary>打开配置文件所在目录。</summary>
    [RelayCommand]
    private void OpenConfigFolder() => HostServices.OpenFolder(ContextMenuService.ConfigFilePath);

    private static string BuildRootKey(string name)
    {
        var cleaned = new string(name.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrEmpty(cleaned) ? "DevEnvMenu" : cleaned;
    }

    #endregion
}

/// <summary>列表中的一行：配置 + 是否已写入注册表。</summary>
public sealed partial class ContextMenuItemViewModel : ObservableObject
{
    public ContextMenuItemViewModel(ContextMenuEntry entry, int index, bool enabled)
    {
        Entry = entry;
        Index = index;
        _isEnabled = enabled;
    }

    public ContextMenuEntry Entry { get; }

    /// <summary>在配置列表中的位置，-1 表示新增。</summary>
    public int Index { get; }

    [ObservableProperty]
    private bool _isEnabled;

    public string Name => Entry.Name;

    public string Root => Entry.Root;

    public string Path => Entry.Path;

    public string ScopeText => Entry.ScopeText;

    public string StateText => IsEnabled ? "已生效" : "未写入注册表";
}