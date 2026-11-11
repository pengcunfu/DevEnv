using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DevEnv.Core.LavaEnv.Models;
using DevEnv.Core.LavaEnv.Services;
using DevEnv.Core.Services;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.Views;

/// <summary>
/// Hosts 编辑器：文本编辑 / 按条编辑双模式，输入时实时校验；
/// 工具条提供定位文件、记事本、环境变量。宿主绑定 <see cref="Text"/> 读写全文。
/// </summary>
public partial class HostsEditorView : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(HostsEditorView),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnTextChanged));

    public static readonly DependencyProperty FooterActionsProperty = DependencyProperty.Register(
        nameof(FooterActions),
        typeof(object),
        typeof(HostsEditorView));

    private bool _issuesExpanded;
    private bool _userToggledIssues;
    private bool _syncing;
    private bool _entryMode;

    public HostsEditorView()
    {
        InitializeComponent();
        Entries = [];
        Entries.CollectionChanged += OnEntriesCollectionChanged;
        Loaded += (_, _) => ValidateAndRender();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>底栏右侧操作区（保存 / 关闭等），由宿主注入。</summary>
    public object? FooterActions
    {
        get => GetValue(FooterActionsProperty);
        set => SetValue(FooterActionsProperty, value);
    }

    public ObservableCollection<HostsEntry> Entries { get; }

    public HostsValidationResult Validation { get; private set; } = HostsFileValidator.Validate(string.Empty);

    /// <summary>保存前调用：若在按条模式，先把表格写回文本。</summary>
    public void CommitPendingEdits()
    {
        if (_entryMode)
            FlushEntriesToText();
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((HostsEditorView)d).OnTextPropertyChanged();

    private void OnTextPropertyChanged()
    {
        if (_syncing)
            return;

        ValidateAndRender();
        if (_entryMode)
            ReloadEntriesFromText();
    }

    private void ValidateAndRender()
    {
        if (StatusText is null || Editor is null)
            return;

        Validation = HostsFileValidator.Validate(Text);
        var issues = Validation.Issues;
        var hasIssues = issues.Count > 0;

        ToggleIssuesButton.Visibility = hasIssues ? Visibility.Visible : Visibility.Collapsed;
        IssueList.ItemsSource = issues;

        if (string.IsNullOrWhiteSpace(Text))
        {
            StatusText.Text = "空文件";
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
            CountText.Text = string.Empty;
        }
        else
        {
            CountText.Text = Validation.EntryCount > 0
                ? $"· {Validation.EntryCount} 条有效记录"
                : "· 无有效记录";

            if (Validation.HasErrors)
            {
                StatusText.Text = Validation.WarningCount > 0
                    ? $"✕ {Validation.ErrorCount} 个错误 · {Validation.WarningCount} 个警告"
                    : $"✕ {Validation.ErrorCount} 个错误";
                StatusText.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush");
            }
            else if (Validation.WarningCount > 0)
            {
                StatusText.Text = $"⚠ {Validation.WarningCount} 个警告";
                StatusText.SetResourceReference(TextBlock.ForegroundProperty, "WarningBrush");
            }
            else
            {
                StatusText.Text = "✓ 格式正常";
                StatusText.SetResourceReference(TextBlock.ForegroundProperty, "SuccessBrush");
            }
        }

        if (!hasIssues)
        {
            _issuesExpanded = false;
            _userToggledIssues = false;
            IssueList.Visibility = Visibility.Collapsed;
            return;
        }

        if (!_userToggledIssues)
            _issuesExpanded = Validation.HasErrors;
        ApplyIssuesVisibility();
    }

    private void ApplyIssuesVisibility()
    {
        IssueList.Visibility = _issuesExpanded ? Visibility.Visible : Visibility.Collapsed;
        ToggleIssuesButton.Content = _issuesExpanded
            ? "收起问题"
            : $"查看问题 ({Validation.Issues.Count})";
    }

    private void OnToggleIssuesClick(object sender, RoutedEventArgs e)
    {
        _userToggledIssues = true;
        _issuesExpanded = !_issuesExpanded;
        ApplyIssuesVisibility();
    }

    private void OnIssueSelected(object sender, SelectionChangedEventArgs e)
    {
        if (IssueList.SelectedItem is not HostsValidationIssue issue)
            return;

        // 问题定位切回文本模式
        if (_entryMode)
        {
            FlushEntriesToText();
            ModeTabs.SelectedIndex = 0;
        }

        if (Editor.LineCount <= 0)
            return;

        var lineIndex = Math.Clamp(issue.LineNumber - 1, 0, Editor.LineCount - 1);
        var start = Editor.GetCharacterIndexFromLineIndex(lineIndex);
        if (start < 0)
            return;

        Editor.Focus();
        Editor.Select(start, 0);
        Editor.ScrollToLine(lineIndex);
        Dispatcher.BeginInvoke(new Action(() => IssueList.SelectedItem = null), DispatcherPriority.Background);
    }

    private void OnModeTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, ModeTabs) || ModeTabs is null)
            return;

        var wantEntry = ModeTabs.SelectedIndex == 1;
        if (wantEntry == _entryMode)
            return;

        if (wantEntry)
        {
            ReloadEntriesFromText();
            _entryMode = true;
        }
        else
        {
            FlushEntriesToText();
            _entryMode = false;
        }
    }

    private void ReloadEntriesFromText()
    {
        _syncing = true;
        try
        {
            UnsubscribeEntryHandlers();
            Entries.Clear();
            foreach (var entry in HostsDocument.ParseEntries(Text))
            {
                entry.PropertyChanged += OnEntryPropertyChanged;
                Entries.Add(entry);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void FlushEntriesToText()
    {
        _syncing = true;
        try
        {
            Text = HostsDocument.ApplyEntries(Text, Entries.ToList());
        }
        finally
        {
            _syncing = false;
        }

        ValidateAndRender();
    }

    private void OnEntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_syncing || !_entryMode)
            return;

        if (e.NewItems is not null)
        {
            foreach (HostsEntry entry in e.NewItems)
                entry.PropertyChanged += OnEntryPropertyChanged;
        }

        if (e.OldItems is not null)
        {
            foreach (HostsEntry entry in e.OldItems)
                entry.PropertyChanged -= OnEntryPropertyChanged;
        }

        FlushEntriesToText();
    }

    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_syncing || !_entryMode)
            return;
        FlushEntriesToText();
    }

    private void UnsubscribeEntryHandlers()
    {
        foreach (var entry in Entries)
            entry.PropertyChanged -= OnEntryPropertyChanged;
    }

    private void OnAddEntryClick(object sender, RoutedEventArgs e)
    {
        if (!_entryMode)
            return;

        var entry = new HostsEntry { Ip = "127.0.0.1", Domain = "example.local", LineNumber = 0 };
        Entries.Add(entry);
        EntryGrid.SelectedItem = entry;
        EntryGrid.ScrollIntoView(entry);
    }

    private void OnRemoveEntryClick(object sender, RoutedEventArgs e)
    {
        if (!_entryMode)
            return;

        var selected = EntryGrid.SelectedItems.Cast<HostsEntry>().ToList();
        if (selected.Count == 0)
            return;

        foreach (var entry in selected)
            Entries.Remove(entry);
    }

    private void OnRevealFileClick(object sender, RoutedEventArgs e) =>
        HostServices.OpenFolder(UtilityService.HostsPath);

    private void OnOpenNotepadClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{UtilityService.HostsPath}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                Window.GetWindow(this),
                ex.Message,
                "无法打开记事本",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnOpenEnvClick(object sender, RoutedEventArgs e)
    {
        try
        {
            SystemTools.OpenEnvironmentVariables();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                Window.GetWindow(this),
                ex.Message,
                "无法打开环境变量",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
