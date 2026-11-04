using System.Windows;
using System.Windows.Controls;
using DevEnv.Host.SigXor.ViewModels;

namespace DevEnv.Host.SigXor.Views;

/// <summary>PDF 预览视图：常驻 PdfWebHost + 错误条。</summary>
public partial class PdfView : UserControl
{
    private PdfContentViewModel? _vm;
    private PdfWebHost? _webHost;
    private string? _loadedPath;

    public PdfView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
        Loaded += OnViewLoaded;
    }

    private void OnViewLoaded(object sender, RoutedEventArgs e) => Refresh();

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = e.NewValue as PdfContentViewModel;
        if (_vm is null) return;

        _vm.PropertyChanged += OnVmPropertyChanged;
        Refresh();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (e.PropertyName == nameof(PdfContentViewModel.Error)
                || e.PropertyName == nameof(PdfContentViewModel.FilePath))
                Refresh();
        });
    }

    private void Refresh()
    {
        if (_vm is null) return;

        ErrorBar.Visibility = string.IsNullOrEmpty(_vm.Error) ? Visibility.Collapsed : Visibility.Visible;

        _webHost ??= WebViewHostPool.RentPdf();
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
            if (ReferenceEquals(WebSlot.Content, _webHost))
                WebSlot.Content = null;
            WebViewHostPool.Return(_webHost);
            _webHost = null;
            _loadedPath = null;
        }
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
    }
}
