using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using DevEnv.Host.SigXor.ViewModels;

namespace DevEnv.Host.SigXor.Views;

public partial class TextView : UserControl
{
    private TextContentViewModel? _vm;
    private bool _loading;

    public TextView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// 语法高亮用的是 VS Code Dark 配色（固定深色方案，见 SyntaxHighlightingService），
    /// 因此编辑器底色必须恒为深色，否则浅色主题下深色语法色会看不清。
    /// 这里显式写死前景 / 背景，不跟主题走——语法配色本身也不跟主题走，两者必须配套。
    /// </summary>
    private static readonly Brush EditorBackground = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
    private static readonly Brush EditorForeground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4));

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            Editor.TextChanged -= OnEditorTextChanged;
        }

        _vm = DataContext as TextContentViewModel;
        if (_vm is null) return;

        _vm.PropertyChanged += OnVmPropertyChanged;
        Editor.TextChanged += OnEditorTextChanged;
        EditControls.Visibility = _vm.AllowEdit ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        // 配色在编辑器实例上设置，随控件生命周期走
        Editor.Background = EditorBackground;
        Editor.Foreground = EditorForeground;

        _loading = true;
        try
        {
            Editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(_vm.Highlighting);
            Editor.Text = _vm.Text;
            Editor.WordWrap = _vm.WordWrap;
            Editor.ScrollToHome();
        }
        finally
        {
            _loading = false;
            _vm.EndLoad();
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TextContentViewModel.WordWrap) && _vm is not null)
        {
            Editor.WordWrap = _vm.WordWrap;
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_loading || _vm is null) return;
        _vm.Text = Editor.Text;
        _vm.MarkDirty();
    }
}
