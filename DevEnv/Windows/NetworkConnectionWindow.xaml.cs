using System.Windows;
using DevEnv.Core.Models;
using DevEnv.Core.Services;

namespace DevEnv.Host.Windows;

/// <summary>
/// 连接详情窗口：非模态，双击流量页的进程行打开；再次双击其他进程时复用并切换内容。
/// 展示的是打开时的快照，需要最新数据时点「刷新」。
/// </summary>
public partial class NetworkConnectionWindow : Window
{
    private static NetworkConnectionWindow? _current;

    private int _processId;
    private string _processName = string.Empty;

    private NetworkConnectionWindow(NetworkProcessView process)
    {
        InitializeComponent();
        Closed += (_, _) =>
        {
            if (ReferenceEquals(_current, this))
                _current = null;
        };
        Apply(process);
    }

    public static void Show(Window? owner, NetworkProcessView process)
    {
        if (_current is { IsLoaded: true })
        {
            _current.Apply(process);
            _current.Activate();
            return;
        }

        var window = new NetworkConnectionWindow(process);
        if (owner is not null && owner.IsVisible)
            window.Owner = owner;
        _current = window;
        window.Show();
    }

    private void Apply(NetworkProcessView process)
    {
        _processId = process.ProcessId;
        _processName = process.DisplayName;
        DataContext = process;
        Title = $"连接详情 - {process.DisplayName} (PID {process.ProcessId})";
        SummaryText.Text =
            $"{process.ConnectionCount} 个连接 · 下载 {process.DownloadRateText} · 上传 {process.UploadRateText}";
        StatusText.Visibility = Visibility.Collapsed;
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        var snapshot = await Task.Run(() => CoreServices.Network.GetSnapshot());
        var process = snapshot.Processes.FirstOrDefault(p => p.ProcessId == _processId);
        if (process is null)
        {
            StatusText.Text = $"{_processName} 已退出，或当前没有网络连接";
            StatusText.Visibility = Visibility.Visible;
            return;
        }

        Apply(process);
    }
}
