using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using DevEnv.Host.SigXor.ViewModels;
using DevEnv.Host.Themes;

namespace DevEnv.Host.SigXor.Views;

public partial class TextView : UserControl
{
    private TextContentViewModel? _vm;
    private bool _loading;
    private bool _themeHooked;

    public TextView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_themeHooked)
        {
            ThemeManager.ThemeChanged += OnThemeChanged;
            _themeHooked = true;
        }
        ApplyTheme();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_themeHooked) return;
        ThemeManager.ThemeChanged -= OnThemeChanged;
        _themeHooked = false;
    }

    private void OnThemeChanged(bool dark) => ApplyTheme();

    /// <summary>
    /// 编辑器底色与前景跟随应用主题；语法高亮定义也按主题取对应配色（VS Code Dark / Light），
    /// 主题切换后重新取一次定义，避免深色配色落在浅色底上。
    /// </summary>
    private void ApplyTheme()
    {
        if (TryFindResource("SurfaceBrush") is Brush background) Editor.Background = background;
        if (TryFindResource("TextPrimaryBrush") is Brush foreground) Editor.Foreground = foreground;
        if (TryFindResource("TextMutedBrush") is Brush lineNumbers) Editor.LineNumbersForeground = lineNumbers;

        if (_vm is not null)
            Editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(_vm.Highlighting);
    }

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

        ApplyTheme();

        _loading = true;
        try
        {
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
