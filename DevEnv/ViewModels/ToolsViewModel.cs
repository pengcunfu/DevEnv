using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core.Models;
using DevEnv.Core.Services;

namespace DevEnv.Host.ViewModels;

/// <summary>系统工具页：按分类分组展示 Windows 系统工具入口。</summary>
public sealed partial class ToolsViewModel : ObservableObject
{
    private static readonly string[] CategoryOrder =
        ["系统管理", "硬件与设备", "网络与安全", "性能与监控"];

    public ToolsViewModel() => Rebuild();

    public ObservableCollection<ToolGroupViewModel> Groups { get; } = [];

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    /// <summary>分类分组（一行标题 + 若干工具）。</summary>
    public sealed class ToolGroupViewModel
    {
        public ToolGroupViewModel(string category, IEnumerable<ToolInfo> tools)
        {
            Category = category;
            Tools = tools.ToList();
        }

        public string Category { get; }
        public IReadOnlyList<ToolInfo> Tools { get; }
    }

    partial void OnQueryChanged(string value) => Rebuild();

    private void Rebuild()
    {
        var all = CoreServices.Shortcuts.GetSystemTools();
        var query = Query.Trim();
        var filtered = query.Length == 0
            ? all
            : all.Where(t =>
                t.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || t.Key.Contains(query, StringComparison.OrdinalIgnoreCase)
                || t.Description.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        var groups = filtered.GroupBy(t => t.Category).ToDictionary(g => g.Key, g => g);
        Groups.Clear();
        foreach (var category in CategoryOrder.Where(groups.ContainsKey))
            Groups.Add(new ToolGroupViewModel(category, groups[category]));
        foreach (var category in groups.Keys.Where(c => !CategoryOrder.Contains(c)))
            Groups.Add(new ToolGroupViewModel(category, groups[category]));
    }

    [RelayCommand]
    public void Run(ToolInfo? tool)
    {
        if (tool is null)
            return;
        var result = CoreServices.Shortcuts.RunSystemTool(tool.Key);
        Message = result.Ok ? $"已启动 {tool.Name}" : result.DisplayMessage;
    }
}