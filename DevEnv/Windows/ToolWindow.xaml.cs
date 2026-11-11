using System.Windows;
using System.Windows.Controls;
using DevEnv.Core.Services;

namespace DevEnv.Host.Windows;

/// <summary>
/// 工具窗口外壳：标题栏由系统提供，内容区放工具视图。
/// 非模态（Show 而非 ShowDialog），归属主窗，随主窗一起最小化/隐藏。
/// 默认尺寸与启动器一致（<see cref="LauncherGeometry"/>）。
/// </summary>
public partial class ToolWindow : Window
{
    public ToolWindow(string title, UserControl content)
    {
        InitializeComponent();
        Title = title;
        Width = LauncherGeometry.Width;
        Height = LauncherGeometry.Height;
        Host.Content = content;
    }
}