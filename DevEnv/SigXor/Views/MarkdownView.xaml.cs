using System.Windows;
using System.Windows.Controls;
using DevEnv.Host.SigXor.ViewModels;

namespace DevEnv.Host.SigXor.Views;

/// <summary>
/// Markdown 双视图：渲染（MarkdownWebHost）与源码（TextView 复用文本编辑栈）切换。
/// 可见性与选中态由 VM 的 UseRendered 驱动（代码后置绑定，仿 OfficeView）。
/// WebView 宿主由 <see cref="WebViewHostPool"/> 常驻复用。
/// </summary>
public partial class MarkdownView : UserControl
{
    private MarkdownContentViewModel? _vm;
    private MarkdownWebHost? _webHost;
    private bool _webLoadedFor;
    private bool _syncing;

    public MarkdownView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
        Loaded += OnViewLoaded;
    }

    private void OnViewLoaded(object sender, RoutedEventArgs e) => RefreshAll();

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = e.NewValue as MarkdownContentViewModel;
        if (_vm is null) return;

        // 源码区挂内部 TextContentViewModel（编辑 / 保存 / 编码全套复用）
        SourceView.DataContext = _vm.Source;
        _vm.PropertyChanged += OnVmPropertyChanged;
        _webLoadedFor = false;
        RefreshAll();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(MarkdownContentViewModel.UseRendered):
                case nameof(MarkdownContentViewModel.Html):
                case nameof(MarkdownContentViewModel.RenderError):
                    RefreshAll();
                    break;
            }
        });
    }

    private void OnRenderToggleChecked(object sender, RoutedEventArgs e)
    {
        if (_syncing || _vm is null) return;
        _vm.UseRendered = true;
    }

    private void OnSourceToggleChecked(object sender, RoutedEventArgs e)
    {
        if (_syncing || _vm is null) return;
        _vm.UseRendered = false;
    }

    private void RefreshAll()
    {
        if (_vm is null) return;

        _syncing = true;
        RenderToggle.IsChecked = _vm.UseRendered;
        SourceToggle.IsChecked = !_vm.UseRendered;
        _syncing = false;

        bool wantWeb = _vm.UseRendered && _vm.CanRender;
        WebSlot.Visibility = wantWeb ? Visibility.Visible : Visibility.Collapsed;
        SourceView.Visibility = wantWeb ? Visibility.Collapsed : Visibility.Visible;

        if (wantWeb)
        {
            _webHost ??= WebViewHostPool.RentMarkdown();
            _webHost.DataContext = _vm;
            // 先 LoadAsync 写入 _basePath，再挂树触发 Configure，保证 md 目录映射就绪
            if (!_webLoadedFor && _vm.Html is not null)
            {
                _ = _webHost.LoadAsync(_vm.FilePath);
                _webLoadedFor = true;
            }
            if (WebSlot.Content != _webHost) WebSlot.Content = _webHost;
        }
        else
        {
            ReleaseHost();
        }
    }

    private void ReleaseHost()
    {
        if (_webHost is null) return;
        if (ReferenceEquals(WebSlot.Content, _webHost))
            WebSlot.Content = null;
        WebViewHostPool.Return(_webHost);
        _webHost = null;
        _webLoadedFor = false;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ReleaseHost();
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
    }
}
