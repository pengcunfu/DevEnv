using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core.TextTools.Models;
using DevEnv.Core.TextTools.Services;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.ViewModels;

/// <summary>文本对比：逐行比较两段文本，标出新增与删除行。</summary>
public sealed partial class TextDiffViewModel : ObservableObject
{
    private const string PageId = "text-diff";
    private readonly TextDiffService _service = new();

    public TextDiffViewModel()
    {
        LeftText = "DevEnv 1.2.0\r\n支持表格视图\r\n支持卡片视图\r\n主题跟随系统";
        RightText = "DevEnv 1.3.0\r\n支持表格视图\r\n支持列表视图\r\n主题跟随系统\r\n新增文本对比工具";
    }

    public ObservableCollection<DiffLine> Lines { get; } = [];

    [ObservableProperty]
    private string _leftText = string.Empty;

    [ObservableProperty]
    private string _rightText = string.Empty;

    [ObservableProperty]
    private bool _ignoreCase;

    [ObservableProperty]
    private bool _ignoreWhitespace;

    [ObservableProperty]
    private string _summary = "把两段文本分别粘贴到左右两侧，然后点击「开始对比」";

    [ObservableProperty]
    private string _error = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private int _addedCount;

    [ObservableProperty]
    private int _removedCount;

    public bool HasResult => Lines.Count > 0;

    [RelayCommand]
    private void Compare()
    {
        var result = _service.Compare(LeftText, RightText, IgnoreCase, IgnoreWhitespace);

        Lines.Clear();
        foreach (var line in result.Lines)
            Lines.Add(line);

        AddedCount = result.AddedCount;
        RemovedCount = result.RemovedCount;
        HasError = !result.Ok;
        Error = result.Error;
        Summary = result.Ok
            ? $"共 {result.Lines.Count} 行 · 新增 {result.AddedCount} 行 · 删除 {result.RemovedCount} 行"
            : "对比失败";

        OnPropertyChanged(nameof(HasResult));
        HostServices.SetStatus(
            result.Ok ? $"文本对比：+{result.AddedCount} / -{result.RemovedCount}" : result.Error,
            PageId);
    }

    [RelayCommand]
    private void Swap()
    {
        (LeftText, RightText) = (RightText, LeftText);
        Compare();
    }

    [RelayCommand]
    private void Clear()
    {
        LeftText = string.Empty;
        RightText = string.Empty;
        Lines.Clear();
        AddedCount = 0;
        RemovedCount = 0;
        Error = string.Empty;
        HasError = false;
        Summary = "把两段文本分别粘贴到左右两侧，然后点击「开始对比」";
        OnPropertyChanged(nameof(HasResult));
        HostServices.SetStatus("已清空", PageId);
    }
}
