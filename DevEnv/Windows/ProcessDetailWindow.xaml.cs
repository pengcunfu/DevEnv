using System.Windows;
using DevEnv.Core.Models;
using DevEnv.Core.Services;

namespace DevEnv.Host.Windows;

/// <summary>
/// 进程详情窗口：非模态，双击进程行打开；再次双击其他进程时复用同一窗口并切换内容。
/// 页面每秒刷新的是列表快照，这里只在手动点「刷新」时重新采样。
/// </summary>
public partial class ProcessDetailWindow : Window
{
    private static ProcessDetailWindow? _current;

    private int _processId;

    private ProcessDetailWindow(ProcessRowView row)
    {
        InitializeComponent();
        Closed += (_, _) =>
        {
            if (ReferenceEquals(_current, this))
                _current = null;
        };
        Apply(row);
    }

    public static void Show(Window? owner, ProcessRowView row)
    {
        if (_current is { IsLoaded: true })
        {
            _current.Apply(row);
            _current.Activate();
            return;
        }

        var window = new ProcessDetailWindow(row);
        if (owner is not null && owner.IsVisible)
            window.Owner = owner;
        _current = window;
        window.Show();
    }

    private void Apply(ProcessRowView row)
    {
        _processId = row.ProcessId;
        DataContext = row;
        Title = $"进程详情 - {row.DisplayName} (PID {row.ProcessId})";
        StatusText.Visibility = Visibility.Collapsed;
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        var snapshot = await Task.Run(() => CoreServices.Processes.GetSnapshot());
        var row = snapshot.Processes.FirstOrDefault(p => p.ProcessId == _processId);
        if (row is null)
        {
            StatusText.Text = "进程已退出或无权访问，无法刷新";
            StatusText.Visibility = Visibility.Visible;
            return;
        }

        Apply(row);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
