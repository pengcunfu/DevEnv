using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core;
using DevEnv.Core.TextTools.Models;
using DevEnv.Core.TextTools.Services;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.ViewModels;

/// <summary>正则测试器：输入表达式与文本，列出每处匹配及捕获组。</summary>
public sealed partial class RegexTesterViewModel : ObservableObject
{
    private const string PageId = "regex";
    private readonly RegexTesterService _service = new();

    public RegexTesterViewModel()
    {
        Pattern = @"(?<order>[A-Z]-\d{4}-\d{3})|([\w.+-]+@[\w-]+\.[\w.]+)";
        Input = "订单号：A-2026-001，邮箱：alice@example.com\r\n"
            + "订单号：B-2026-002，邮箱：bob@test.org\r\n"
            + "没有匹配的一行";
    }

    public ObservableCollection<RegexMatchItem> Matches { get; } = [];

    [ObservableProperty]
    private string _pattern = string.Empty;

    [ObservableProperty]
    private string _input = string.Empty;

    [ObservableProperty]
    private bool _ignoreCase;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MultilineHint))]
    private bool _multiline = true;

    public string MultilineHint => Multiline ? "多行：^ 与 $ 匹配每行" : "单行：^ 与 $ 只匹配整段文本";

    [ObservableProperty]
    private bool _singleline;

    [ObservableProperty]
    private string _summary = "输入表达式与文本后点击「匹配」（Ctrl+Enter）";

    [ObservableProperty]
    private string _error = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    public bool HasMatches => Matches.Count > 0;

    [RelayCommand]
    private void Test()
    {
        var options = RegexOptions.None;
        if (IgnoreCase)
            options |= RegexOptions.IgnoreCase;
        if (Multiline)
            options |= RegexOptions.Multiline;
        if (Singleline)
            options |= RegexOptions.Singleline;

        var result = _service.Test(Pattern, Input, options);

        Matches.Clear();
        foreach (var item in result.Matches)
            Matches.Add(item);

        HasError = !result.Ok;
        Error = result.Error;
        Summary = result.Ok
            ? $"匹配 {result.Count} 处 · {result.ElapsedMs} ms" + (result.Truncated ? "（已截断到 2000 条）" : string.Empty)
            : "表达式有误，请检查下方提示";

        OnPropertyChanged(nameof(HasMatches));
        HostServices.SetStatus(result.Ok ? $"正则匹配：{result.Count} 处" : result.Error, PageId);
    }

    [RelayCommand]
    private void Clear()
    {
        Pattern = string.Empty;
        Input = string.Empty;
        Matches.Clear();
        Error = string.Empty;
        HasError = false;
        Summary = "输入表达式与文本后点击「匹配」（Ctrl+Enter）";
        OnPropertyChanged(nameof(HasMatches));
        HostServices.SetStatus("已清空", PageId);
    }

    [RelayCommand]
    private void CopyMatches()
    {
        if (Matches.Count == 0)
        {
            HostServices.SetStatus("没有可复制的匹配结果", PageId);
            return;
        }

        var text = string.Join(Environment.NewLine, Matches.Select(match => match.Value));
        HostCallbacks.SetClipboardText?.Invoke(text);
        HostServices.SetStatus($"已复制 {Matches.Count} 条匹配结果", PageId);
    }
}
