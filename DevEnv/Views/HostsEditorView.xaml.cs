using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DevEnv.Core.LavaEnv.Models;
using DevEnv.Core.LavaEnv.Services;

namespace DevEnv.Host.Views;

/// <summary>
/// Hosts 文本编辑框：输入时实时校验语法与条目，并允许点击问题定位到对应行。
/// 宿主只需绑定 <see cref="Text"/>，保存时读同一属性。
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

    /// <summary>问题列表当前是否展开，以及用户是否手动干预过自动展开策略。</summary>
    private bool _issuesExpanded;
    private bool _userToggledIssues;

    public HostsEditorView()
    {
        InitializeComponent();
        Loaded += (_, _) => ValidateAndRender();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public HostsValidationResult Validation { get; private set; } = HostsFileValidator.Validate(string.Empty);

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((HostsEditorView)d).ValidateAndRender();

    private void ValidateAndRender()
    {
        // 绑定可能在 InitializeComponent 之前触发，控件尚未就绪时交给 Loaded 补一次。
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

        // 有错误时默认展开问题列表；只有警告时默认收起，避免占满编辑区。
        // 用户手动收起/展开后不再自动改变。
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
        if (IssueList.SelectedItem is not HostsValidationIssue issue || Editor.LineCount <= 0)
            return;

        var lineIndex = Math.Clamp(issue.LineNumber - 1, 0, Editor.LineCount - 1);
        var start = Editor.GetCharacterIndexFromLineIndex(lineIndex);
        if (start < 0)
            return;

        Editor.Focus();
        Editor.Select(start, 0);
        Editor.ScrollToLine(lineIndex);

        // 立即取消选中，保证再次点击同一条问题也能重新定位。
        Dispatcher.BeginInvoke(new Action(() => IssueList.SelectedItem = null), DispatcherPriority.Background);
    }
}
