using System.Windows;
using DevEnv.Core.NetworkWatch.Models;

namespace DevEnv.Host.Windows;

/// <summary>端口管理的进程详情：非模态，再次打开时复用同一窗口。</summary>
public partial class PortProcessDetailWindow : Window
{
    private static PortProcessDetailWindow? _current;

    private PortProcessDetailWindow(ProcessDetailInfo detail)
    {
        InitializeComponent();
        Closed += (_, _) =>
        {
            if (ReferenceEquals(_current, this))
                _current = null;
        };
        Apply(detail);
    }

    public static void Show(Window? owner, ProcessDetailInfo detail)
    {
        if (_current is { IsLoaded: true })
        {
            _current.Apply(detail);
            _current.Activate();
            return;
        }

        var window = new PortProcessDetailWindow(detail);
        if (owner is not null && owner.IsVisible)
            window.Owner = owner;
        _current = window;
        window.Show();
    }

    private void Apply(ProcessDetailInfo detail)
    {
        DataContext = detail;
        var name = string.IsNullOrWhiteSpace(detail.Name) ? "未知进程" : detail.Name;
        TitleText.Text = name;
        SubtitleText.Text = string.IsNullOrWhiteSpace(detail.ErrorMessage)
            ? $"PID {detail.Pid} · {detail.Protocol} {detail.LocalEndpoint}"
            : $"PID {detail.Pid}";
        Title = $"进程详情 - {name} (PID {detail.Pid})";

        var hasError = !string.IsNullOrWhiteSpace(detail.ErrorMessage);
        ErrorText.Text = detail.ErrorMessage;
        ErrorText.Visibility = hasError ? Visibility.Visible : Visibility.Collapsed;
        DetailPanel.Visibility = hasError ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
