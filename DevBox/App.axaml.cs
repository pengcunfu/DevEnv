using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ProgramBox.Utils;

namespace ProgramBox
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // 必须尽早设置：否则隐藏主窗口会导致整个进程退出
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                var mainWindow = new MainWindow();
                desktop.MainWindow = mainWindow;
                mainWindow.InitializeTray();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}