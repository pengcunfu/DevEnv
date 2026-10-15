using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using DevEnv;
using DevEnv.Services;
using DevEnv.Views;
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
                var editHosts = Environment.GetCommandLineArgs()
                    .Any(a => string.Equals(a, ElevationHelper.EditHostsArgument, StringComparison.OrdinalIgnoreCase));

                if (editHosts)
                {
                    // 提权后的独立 Hosts 编辑进程：关闭窗口即退出，不走托盘/单实例主窗
                    desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    DevEnvBootstrap.EnsureInitialized();
                    desktop.MainWindow = new HostsFileEditWindow();
                    base.OnFrameworkInitializationCompleted();
                    return;
                }

                // 必须尽早设置：否则隐藏主窗口会导致整个进程退出
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                DevEnvBootstrap.EnsureInitialized();

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
