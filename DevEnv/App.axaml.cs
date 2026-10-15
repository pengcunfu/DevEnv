using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DevEnv.Models;
using DevEnv.Services;
using DevEnv.Views;

namespace DevEnv;

public partial class App : Application
{
    static App()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AppPaths.EnsureDirectories();
        AppServices.Config.Load();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (Environment.GetCommandLineArgs().Contains(ElevationHelper.EditHostsArgument, StringComparer.OrdinalIgnoreCase))
            {
                desktop.MainWindow = new HostsFileEditWindow();
            }
            else
            {
                desktop.MainWindow = new MainWindow();
            }

            desktop.MainWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
