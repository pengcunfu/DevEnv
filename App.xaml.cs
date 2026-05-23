using System.Text;
using System.Windows;
using DevEnv.Models;
using DevEnv.Services;
using DevEnv.Views;

namespace DevEnv
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static App()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            AppPaths.EnsureDirectories();
            AppServices.Config.Load();

            if (e.Args.Contains(ElevationHelper.EditHostsArgument, StringComparer.OrdinalIgnoreCase))
            {
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                var window = new HostsFileEditWindow();
                MainWindow = window;
                window.Show();
                return;
            }

            ShutdownMode = ShutdownMode.OnMainWindowClose;
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
    }
}
