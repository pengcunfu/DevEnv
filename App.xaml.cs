using System.Configuration;
using System.Data;
using System.Text;
using System.Windows;
using DevEnv.Models;
using DevEnv.Services;

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
            base.OnStartup(e);
        }
    }

}
