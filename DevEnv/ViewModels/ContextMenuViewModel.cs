using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core;
using DevEnv.Core.Models;
using DevEnv.Core.Services;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.ViewModels;

/// <summary>
/// 右键菜单管理：左侧配置列表 + 右侧详情，顶部菜单提供增删、
/// Win11 一级/二级菜单切换与环境检查。配置在
/// 文档\FNSoftware\DevEnv\ContextMenu\menu_configs.json。
/// </summary>
public sealed partial class ContextMenuViewModel : ObservableObject
{
    private const string PageId = "context-menu";

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

    [RelayCommand]
    public void Reload()
    {
        var service = CoreServices.ContextMenu;
        var keepRoot = Selected?.Root;

        IsWindows11 = service.IsWindows11;
        IsClassicMenu = service.IsWin11MenuDisabled;
        Win11Status = service.Win11StatusText;

        Entries.Clear();
        var index = 0;
        ContextMenuItemViewModel? restore = null;
        foreach (var entry in service.GetEntries())
        {
            var item = new ContextMenuItemViewModel(CloneEntry(entry), index, service.IsEntryEnabled(entry));
            Entries.Add(item);
            if (restore is null
                && !string.IsNullOrWhiteSpace(keepRoot)
                && string.Equals(item.Root, keepRoot, StringComparison.OrdinalIgnoreCase))
            {
                restore = item;
            }

            index++;
        }

        Selected = restore ?? Entries.FirstOrDefault();
        Report(Entries.Count == 0 ? "暂无配置，可从「工具 → 添加配置」新建" : $"已加载 {Entries.Count} 条配置 · {Win11Status}");
    }

    [RelayCommand]
    private void UseClassicMenu() => ApplyWin11Menu(useClassic: true);

    [RelayCommand]
    private void UseDefaultMenu() => ApplyWin11Menu(useClassic: false);

    private void ApplyWin11Menu(bool useClassic)
    {
        var result = CoreServices.ContextMenu.SetWin11ClassicMenu(useClassic);
        IsWindows11 = CoreServices.ContextMenu.IsWindows11;
        IsClassicMenu = CoreServices.ContextMenu.IsWin11MenuDisabled;
        Win11Status = CoreServices.ContextMenu.Win11StatusText;
        Report(result.DisplayMessage);
    }

    #endregion

    #region 主从编辑

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveOnlyCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestPathCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseProgramCommand))]
    private ContextMenuItemViewModel? _selected;

    public bool HasSelection => Selected is not null;

    [RelayCommand]
    private void NewEntry()
    {
        // 草稿必须进 ItemsSource，否则 ListBox 双向绑定会把 Selected 冲掉
        foreach (var draft in Entries.Where(e => e.Index < 0).ToList())
            Entries.Remove(draft);

        var item = new ContextMenuItemViewModel(
            new ContextMenuEntry
            {
                Name = "新菜单项",
                ForFiles = true,
                ForDirectories = true,
                ForDesktop = true,
            },
            index: -1,
            enabled: false);
        Entries.Add(item);
        Selected = item;
        Report("填写右侧详情后点「保存配置」或「应用更改」");
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void BrowseProgram()
    {
        if (Selected is null)
            return;
        var path = HostCallbacks.PickExecutable?.Invoke();
        if (string.IsNullOrWhiteSpace(path))
            return;

        Selected.Path = path;
        if (string.IsNullOrWhiteSpace(Selected.Name))
            Selected.Name = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(Selected.Root))
            Selected.Root = CoreServices.ContextMenu.GenerateUniqueRoot(Selected.Name);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Save() => SaveCore(applyToSystem: true);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void SaveOnly() => SaveCore(applyToSystem: false);

    private void SaveCore(bool applyToSystem)
    {
        if (Selected is null)
            return;

        var service = CoreServices.ContextMenu;
        var index = Selected.Index;
        var entry = Selected.ToEntry();

        if (string.IsNullOrWhiteSpace(entry.Root))
            entry.Root = service.GenerateUniqueRoot(entry.Name);

        var previous = index >= 0 ? service.GetEntries().ElementAtOrDefault(index) : null;
        if (previous is not null
            && !string.Equals(previous.Root, entry.Root, StringComparison.OrdinalIgnoreCase))
        {
            _ = service.RemoveEntryFromSystem(previous);
        }

        var saved = service.SaveEntry(entry, index >= 0 ? index : null);
        if (!saved.Ok)
        {
            Report(saved.DisplayMessage);
            return;
        }

        if (applyToSystem)
        {
            var applied = service.ApplyEntry(entry);
            Report(applied.Ok ? $"{entry.Name}：{applied.DisplayMessage}" : applied.DisplayMessage);
        }
        else
        {
            Report($"{entry.Name}：配置已保存（未写入注册表）");
        }

        var keepRoot = entry.Root;
        Reload();
        Selected = Entries.FirstOrDefault(e =>
            string.Equals(e.Root, keepRoot, StringComparison.OrdinalIgnoreCase));
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DeleteSelected()
    {
        if (Selected is null)
            return;

        if (Selected.Index < 0)
        {
            Entries.Remove(Selected);
            Selected = Entries.FirstOrDefault();
            Report("已取消新建");
            return;
        }

        if (!Windows.ConfirmDialog.Show(HostServices.DialogOwner, $"删除「{Selected.Name}」并从注册表移除？", "删除"))
            return;

        var service = CoreServices.ContextMenu;
        var removed = service.RemoveEntryFromSystem(Selected.ToEntry());
        var deleted = service.DeleteEntry(Selected.Index);

        Report(deleted.Ok
            ? (removed.Ok ? $"{Selected.Name}：已删除" : $"配置已删除，注册表移除失败：{removed.DisplayMessage}")
            : deleted.DisplayMessage);

        Reload();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void TestPath()
    {
        if (Selected is null)
            return;

        var path = Selected.Path;
        Report(string.IsNullOrWhiteSpace(path)
            ? "请先填写程序路径"
            : File.Exists(path) ? $"路径有效：{path}" : $"路径不存在：{path}");
    }

    [RelayCommand]
    private void CheckEnvironment() => Report(CoreServices.ContextMenu.GetStatusSummary());

    [RelayCommand]
    private void ShowAbout()
    {
        System.Windows.MessageBox.Show(
            HostServices.DialogOwner,
            "右键菜单管理\n\n"
            + "· 自定义「用 XXX 打开」等资源管理器右键菜单项\n"
            + "· 支持文件 / 目录 / 桌面作用域\n"
            + "· 可切换 Windows 11 一级菜单与完整二级菜单\n\n"
            + $"配置文件：\n{ContextMenuService.ConfigFilePath}\n\n"
            + "写入注册表需要管理员权限。",
            "关于",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void Report(string message) => HostServices.SetStatus(message, PageId);

    private static ContextMenuEntry CloneEntry(ContextMenuEntry source) => new()
    {
        Name = source.Name,
        Root = source.Root,
        Path = source.Path,
        ForFiles = source.ForFiles,
        ForDirectories = source.ForDirectories,
        ForDesktop = source.ForDesktop,
    };

    #endregion
}

/// <summary>列表中的一行：配置 + 是否已写入注册表。</summary>
public sealed partial class ContextMenuItemViewModel : ObservableObject
{
    public ContextMenuItemViewModel(ContextMenuEntry entry, int index, bool enabled)
    {
        _name = entry.Name;
        _root = entry.Root;
        _path = entry.Path;
        _forFiles = entry.ForFiles;
        _forDirectories = entry.ForDirectories;
        _forDesktop = entry.ForDesktop;
        Index = index;
        _isEnabled = enabled;
    }

    /// <summary>在配置列表中的位置，-1 表示新增未落盘。</summary>
    public int Index { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _root = string.Empty;

    [ObservableProperty]
    private string _path = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(ScopeText))]
    private bool _forFiles;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(ScopeText))]
    private bool _forDirectories;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(ScopeText))]
    private bool _forDesktop;

    public string ScopeText
    {
        get
        {
            var parts = new List<string>();
            if (ForFiles) parts.Add("文件");
            if (ForDirectories) parts.Add("目录");
            if (ForDesktop) parts.Add("桌面");
            return parts.Count == 0 ? "未设置" : string.Join(" + ", parts);
        }
    }

    public string StatusText => IsEnabled
        ? $"✓ 已启用 ({ScopeText})"
        : "○ 未启用";

    public ContextMenuEntry ToEntry() => new()
    {
        Name = Name,
        Root = Root,
        Path = Path,
        ForFiles = ForFiles,
        ForDirectories = ForDirectories,
        ForDesktop = ForDesktop,
    };
}
