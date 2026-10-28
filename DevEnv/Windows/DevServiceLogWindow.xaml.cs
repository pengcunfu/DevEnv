using System.Text;
using System.Windows;
using System.Windows.Threading;
using DevEnv.Core.Services;

namespace DevEnv.Host.Windows;

/// <summary>服务运行日志窗口：非模态，定时拉取最新日志，切服务时复用同一窗口。</summary>
public partial class DevServiceLogWindow : Window
{
    private static DevServiceLogWindow? _current;

    private readonly DevEnvService _service;
    private readonly DispatcherTimer _timer;

    private string _serviceId;
    private string _lastText = string.Empty;

    private DevServiceLogWindow(DevEnvService service, string serviceId, string name)
    {
        InitializeComponent();
        _service = service;
        _serviceId = serviceId;
        Title = $"{name} 运行日志";

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) =>
        {
            Refresh();
            _timer.Start();
        };
        Closed += (_, _) =>
        {
            _timer.Stop();
            if (ReferenceEquals(_current, this))
                _current = null;
        };
    }

    public static void Show(Window? owner, DevEnvService service, string serviceId, string name)
    {
        if (_current is { IsLoaded: true })
        {
            _current.Retarget(serviceId, name);
            _current.Activate();
            return;
        }

        var window = new DevServiceLogWindow(service, serviceId, name);
        if (owner is not null && owner.IsVisible)
            window.Owner = owner;
        _current = window;
        window.Show();
    }

    private void Retarget(string serviceId, string name)
    {
        _serviceId = serviceId;
        Title = $"{name} 运行日志";
        _lastText = string.Empty;
        Refresh();
    }

    private void Refresh()
    {
        var text = _service.GetLogText(_serviceId);
        if (string.Equals(text, _lastText, StringComparison.Ordinal))
            return;

        _lastText = text;

        // 不跟随时记住当前首行，刷新后恢复位置，避免自动跳到末尾。
        var following = FollowCheckBox.IsChecked == true;
        var firstVisibleLine = following ? -1 : LogBox.GetFirstVisibleLineIndex();

        LogBox.Text = WithLineNumbers(text);

        if (following)
            LogBox.ScrollToEnd();
        else if (firstVisibleLine >= 0)
            LogBox.ScrollToLine(firstVisibleLine);
    }

    /// <summary>每行前面补行号，长行由 TextWrapping 自动折行。</summary>
    private static string WithLineNumbers(string text)
    {
        if (text.Length == 0)
            return string.Empty;

        var lines = text.Split('\n');
        var builder = new StringBuilder(text.Length + lines.Length * 6);
        for (var i = 0; i < lines.Length; i++)
            builder.Append((i + 1).ToString().PadLeft(4)).Append("  ").AppendLine(lines[i].TrimEnd('\r'));
        return builder.ToString().TrimEnd();
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => Refresh();

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        _service.ClearLog(_serviceId);
        _lastText = string.Empty;
        LogBox.Clear();
    }

    private void OnOpenDirectoryClick(object sender, RoutedEventArgs e) =>
        _service.OpenLogsDirectory();
}
