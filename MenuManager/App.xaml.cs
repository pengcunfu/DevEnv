using System.Windows;

using MenuManager.Services;

namespace MenuManager
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 提权操作模式：执行注册表操作后直接退出，不显示主窗口
            var args = Environment.GetCommandLineArgs();
            var opIndex = Array.IndexOf(args, "--elevated-op");
            if (opIndex >= 0 && opIndex + 1 < args.Length)
            {
                var exitCode = ElevatedRegistryService.ExecuteFromCommandLine(args[opIndex + 1]);
                Shutdown(exitCode);
                return;
            }

            // 设置全局异常处理
            DispatcherUnhandledException += (sender, ex) =>
            {
                MessageBox.Show($"发生未处理的异常: {ex.Exception.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true;
            };

            // 创建并显示主窗口
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
