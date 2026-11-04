using System.Windows;
using System.Windows.Controls;
using DevEnv.Host.SigXor.ViewModels;

namespace DevEnv.Host.SigXor.Views;

public partial class SvgView : UserControl
{
    private SvgContentViewModel? _vm;
    private SvgWebHost? _webHost;
    private string? _loadedPath;

    public SvgView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
        Loaded += (_, _) => Refresh();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
        _vm = e.NewValue as SvgContentViewModel;
        if (_vm is null) return;
        _vm.PropertyChanged += OnVmChanged;
        Refresh();
    }

    private void OnVmChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(Refresh);
    }

    private void Refresh()
    {
        if (_vm is null) return;
        ErrorBar.Visibility = string.IsNullOrEmpty(_vm.Error) ? Visibility.Collapsed : Visibility.Visible;

        if (!WebViewHostBase.IsRuntimeAvailable())
        {
            _vm.Error ??= "SVG 预览需要 WebView2 运行时。";
            ErrorBar.Visibility = Visibility.Visible;
            return;
        }

        _webHost ??= WebViewHostPool.RentSvg();
        _webHost.DataContext = _vm;
        if (!string.Equals(_loadedPath, _vm.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            _ = _webHost.LoadAsync(_vm.FilePath);
            _loadedPath = _vm.FilePath;
        }
        if (WebSlot.Content != _webHost) WebSlot.Content = _webHost;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_webHost is not null)
        {
            if (ReferenceEquals(WebSlot.Content, _webHost)) WebSlot.Content = null;
            WebViewHostPool.Return(_webHost);
            _webHost = null;
            _loadedPath = null;
        }
        if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
    }
}
