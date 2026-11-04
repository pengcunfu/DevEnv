using System.Windows;
using System.Windows.Controls;
using DevEnv.Host.SigXor.ViewModels;

namespace DevEnv.Host.SigXor.Views;

/// <summary>音频预览视图：常驻 AudioWebHost 播放页 + 错误条。</summary>
public partial class AudioView : UserControl
{
    private AudioContentViewModel? _vm;
    private AudioWebHost? _webHost;
    private string? _loadedPath;

    public AudioView()
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
        _vm = e.NewValue as AudioContentViewModel;
        if (_vm is null) return;

        _vm.PropertyChanged += OnVmPropertyChanged;
        Refresh();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (e.PropertyName == nameof(AudioContentViewModel.Error)
                || e.PropertyName == nameof(AudioContentViewModel.FilePath))
                Refresh();
        });
    }

    private void Refresh()
    {
        if (_vm is null) return;

        ErrorBar.Visibility = string.IsNullOrEmpty(_vm.Error) ? Visibility.Collapsed : Visibility.Visible;

        _webHost ??= WebViewHostPool.RentAudio();
        _webHost.DataContext = _vm;
        if (!string.Equals(_loadedPath, _vm.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            _ = _webHost.LoadAsync(_vm.FilePath, _vm.Name, _vm.Length);
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
