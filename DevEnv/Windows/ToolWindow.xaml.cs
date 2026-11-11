using System.Windows;
using System.Windows.Controls;
using DevEnv.Core.Services;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.Windows;

/// <summary>
/// 工具窗口外壳：标题栏由系统提供，内容区放工具视图，底部状态栏承接 HostServices 反馈。
/// 非模态（Show 而非 ShowDialog），归属主窗，随主窗一起最小化/隐藏。
/// 默认尺寸与启动器一致（<see cref="LauncherGeometry"/>）。
/// </summary>
public partial class ToolWindow : Window
{
    public ToolWindow(string toolId, string title, UserControl content)
    {
        InitializeComponent();
        ToolId = toolId;
        Title = title;
        Width = LauncherGeometry.Width;
        Height = LauncherGeometry.Height;
        VersionTextBlock.Text = $"版本 {HostServices.VersionText}";
        Host.Content = content;
    }

    /// <summary>工具 id，与 <see cref="ToolWindowHost"/> 注册表一致，供状态栏路由。</summary>
    public string ToolId { get; }

    public void SetStatus(string message) =>
        StatusTextBlock.Text = string.IsNullOrWhiteSpace(message) ? "就绪" : message;

    public void SetPageStatus(string message)
    {
        var text = message?.Trim() ?? string.Empty;
        PageStatusTextBlock.Text = text;
        PageStatusTextBlock.Visibility = string.IsNullOrEmpty(text)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}
