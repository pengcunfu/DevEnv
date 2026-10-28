using System.IO;
using System.Windows;
using DevEnv.Core;
using DevEnv.Core.Models;

namespace DevEnv.Host.Windows;

/// <summary>新增 / 编辑开发环境服务配置。取消返回 null。</summary>
public partial class DevServiceEditWindow : Window
{
    private readonly string _id;

    private DevServiceEditWindow(DevServiceConfig? config)
    {
        InitializeComponent();
        _id = config?.Id ?? string.Empty;
        Title = config is null ? "添加服务" : "编辑服务";
        NameBox.Text = config?.Name ?? string.Empty;
        WorkDirBox.Text = config?.WorkingDirectory ?? string.Empty;
        StartCommandBox.Text = config?.StartCommand ?? string.Empty;
        StopCommandBox.Text = config?.StopCommand ?? string.Empty;
        ProcessNameBox.Text = config?.ProcessName ?? string.Empty;
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    public DevServiceConfig? Result { get; private set; }

    public static DevServiceConfig? Show(Window? owner, DevServiceConfig? config = null)
    {
        var dialog = new DevServiceEditWindow(config);
        if (owner is not null && owner.IsVisible)
            dialog.Owner = owner;
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var folder = HostCallbacks.PickFolder?.Invoke();
        if (!string.IsNullOrWhiteSpace(folder))
            WorkDirBox.Text = folder;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (name.Length == 0)
        {
            ShowError("请填写服务名称");
            return;
        }

        var startCommand = StartCommandBox.Text.Trim();
        if (startCommand.Length == 0)
        {
            ShowError("请填写启动命令");
            return;
        }

        var workingDirectory = WorkDirBox.Text.Trim().Trim('"');
        if (workingDirectory.Length > 0 && !Directory.Exists(workingDirectory))
        {
            ShowError($"工作目录不存在：{workingDirectory}");
            return;
        }

        Result = new DevServiceConfig
        {
            Id = _id,
            Name = name,
            WorkingDirectory = workingDirectory,
            StartCommand = startCommand,
            StopCommand = StopCommandBox.Text.Trim(),
            ProcessName = ProcessNameBox.Text.Trim(),
        };
        DialogResult = true;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
