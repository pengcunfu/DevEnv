using System.Windows;
using System.Windows.Controls;
using DevEnv.Host.SigXor.ViewModels;

namespace DevEnv.Host.SigXor.Views;

/// <summary>
/// 网页双视图：渲染（HtmlWebHost，目录映射 + 脚本启用）与只读源码（TextView 懒加载）切换。
/// WebView 宿主由 <see cref="WebViewHostPool"/> 常驻复用。
/// </summary>
public partial class HtmlView : UserControl
{
    private WebContentViewModel? _vm;
    private HtmlWebHost? _webHost;
    private bool _webLoadedFor;
    private bool _syncing;

    public HtmlView()
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
        _vm = e.NewValue as WebContentViewModel;
        if (_vm is null) return;

        _vm.PropertyChanged += OnVmPropertyChanged;
        _webLoadedFor = false;
        RefreshAll();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (e.PropertyName == nameof(WebContentViewModel.UseRendered)
                || e.PropertyName == nameof(WebContentViewModel.RenderError))
                RefreshAll();
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
        RenderToggle.IsEnabled = _vm.CanRender;
        _syncing = false;

        bool wantWeb = _vm.UseRendered && _vm.CanRender;
        WebSlot.Visibility = wantWeb ? Visibility.Visible : Visibility.Collapsed;
        SourceView.Visibility = wantWeb ? Visibility.Collapsed : Visibility.Visible;

        if (wantWeb)
        {
            if (HtmlWebHost.IsPathRenderable(_vm.FilePath))
            {
                _webHost ??= WebViewHostPool.RentHtml();
                _webHost.DataContext = _vm;
                if (!_webLoadedFor)
                {
                    _ = _webHost.LoadAsync(_vm.FilePath);
                    _webLoadedFor = true;
                }
                if (WebSlot.Content != _webHost) WebSlot.Content = _webHost;
            }
        }
        else
        {
            // 源码懒加载：首次进入才读取文件
            if (!ReferenceEquals(SourceView.DataContext, _vm.Source))
                SourceView.DataContext = _vm.GetSource();
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
