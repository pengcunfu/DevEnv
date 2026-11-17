using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core;
using DevEnv.Core.FileTools.Models;
using DevEnv.Core.FileTools.Services;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.ViewModels;

/// <summary>批量重命名：预览规则结果，确认后再落盘。</summary>
public sealed partial class BatchRenameViewModel : ObservableObject
{
    private const string PageId = "batch-rename";
    private readonly BatchRenameService _service = new();

    public BatchRenameViewModel()
    {
        Preview();
    }

    public ObservableCollection<BatchRenameItem> Items { get; } = [];

    public IReadOnlyList<string> CaseModes { get; } = ["保持原样", "全部大写", "全部小写"];

    [ObservableProperty]
    private string _folderPath = string.Empty;

    [ObservableProperty]
    private string _prefix = string.Empty;

    [ObservableProperty]
    private string _suffix = string.Empty;

    [ObservableProperty]
    private string _findText = string.Empty;

    [ObservableProperty]
    private string _replaceText = string.Empty;

    [ObservableProperty]
    private bool _useNumbering;

    [ObservableProperty]
    private int _startNumber = 1;

    [ObservableProperty]
    private int _step = 1;

    [ObservableProperty]
    private int _numberWidth = 2;

    [ObservableProperty]
    private string _numberSeparator = "_";

    [ObservableProperty]
    private bool _includeExtension;

    [ObservableProperty]
    private int _caseModeIndex;

    [ObservableProperty]
    private string _summary = "选择一个文件夹后点击「预览」";

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private int _changedCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private int _errorCount;

    public bool CanApply => ChangedCount > 0 && ErrorCount == 0;

    partial void OnFolderPathChanged(string value) => InvalidatePreview();

    partial void OnPrefixChanged(string value) => InvalidatePreview();

    partial void OnSuffixChanged(string value) => InvalidatePreview();

    partial void OnFindTextChanged(string value) => InvalidatePreview();

    partial void OnReplaceTextChanged(string value) => InvalidatePreview();

    partial void OnUseNumberingChanged(bool value) => InvalidatePreview();

    partial void OnStartNumberChanged(int value) => InvalidatePreview();

    partial void OnStepChanged(int value) => InvalidatePreview();

    partial void OnNumberWidthChanged(int value) => InvalidatePreview();

    partial void OnNumberSeparatorChanged(string value) => InvalidatePreview();

    partial void OnIncludeExtensionChanged(bool value) => InvalidatePreview();

    partial void OnCaseModeIndexChanged(int value) => InvalidatePreview();

    /// <summary>规则一改就丢掉旧预览，避免把上一套规则算出的名字直接落盘。</summary>
    private void InvalidatePreview()
    {
        Items.Clear();
        ChangedCount = 0;
        ErrorCount = 0;
        Summary = string.IsNullOrWhiteSpace(FolderPath)
            ? "请选择要重命名文件的文件夹"
            : "规则已修改，请重新预览";
    }

    [RelayCommand]
    private void PickFolder()
    {
        var folder = HostCallbacks.PickFolder?.Invoke();
        if (string.IsNullOrWhiteSpace(folder))
            return;

        FolderPath = folder;
        Preview();
    }

    [RelayCommand]
    private void Preview()
    {
        Items.Clear();
        Message = string.Empty;
        HasError = false;

        if (string.IsNullOrWhiteSpace(FolderPath) || !System.IO.Directory.Exists(FolderPath))
        {
            ChangedCount = 0;
            ErrorCount = 0;
            Summary = "请选择要重命名文件的文件夹";
            return;
        }

        var preview = _service.Preview(FolderPath, BuildOptions());
        foreach (var item in preview.Items)
            Items.Add(item);

        ChangedCount = preview.ChangedCount;
        ErrorCount = preview.ErrorCount;
        Summary = preview.TotalCount == 0
            ? "该文件夹内没有文件"
            : $"共 {preview.TotalCount} 个文件 · 将重命名 {preview.ChangedCount} 个"
                + (preview.ErrorCount > 0 ? $" · {preview.ErrorCount} 个有冲突" : string.Empty);

        HostServices.SetStatus(Summary, PageId);
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply()
    {
        var changed = ChangedCount;
        if (MessageBox.Show(
                $"确定重命名 {changed} 个文件吗？\n\n此操作会直接修改磁盘上的文件名。",
                "批量重命名",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question) != MessageBoxResult.OK)
        {
            return;
        }

        var result = _service.Apply(FolderPath, Items.ToList());
        HasError = !result.Ok;
        Message = result.DisplayMessage;
        HostServices.SetStatus(result.DisplayMessage, PageId);

        if (result.Ok)
        {
            // 落盘后不再按同一套规则预览，避免把刚改好的名字再套一遍。
            Items.Clear();
            ChangedCount = 0;
            ErrorCount = 0;
            Summary = result.DisplayMessage;
        }
    }

    private BatchRenameOptions BuildOptions() => new()
    {
        Prefix = Prefix,
        Suffix = Suffix,
        Find = FindText,
        Replace = ReplaceText,
        UseNumbering = UseNumbering,
        StartNumber = StartNumber,
        Step = Step,
        NumberWidth = NumberWidth,
        NumberSeparator = NumberSeparator,
        IncludeExtension = IncludeExtension,
        CaseMode = CaseModeIndex switch
        {
            1 => RenameCaseMode.Upper,
            2 => RenameCaseMode.Lower,
            _ => RenameCaseMode.Keep,
        },
    };
}
