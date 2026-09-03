using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
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

                // 应用保存的深色/浅色主题设置
                var configManager = new JsonDataManager();
                RequestedThemeVariant = configManager.Data.Config.DarkMode ? ThemeVariant.Dark : ThemeVariant.Light;

                var mainWindow = new MainWindow();
                desktop.MainWindow = mainWindow;
                mainWindow.InitializeTray();
                SingleInstanceService.StartActivateListener(mainWindow);

                // Velopack 更新后重启：延迟到窗口就绪后在底部状态条提示
                if (StartupHooks.UpdatedTo is { } updatedTo)
                {
                    Dispatcher.UIThread.Post(async () =>
                    {
                        await Task.Delay(800);
                        await AppHelper.ShowInfoAsync($"已更新到 {updatedTo}");
                    });
                }
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
