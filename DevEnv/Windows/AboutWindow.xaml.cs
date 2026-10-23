using System.Windows;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.Windows;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"版本 {HostServices.VersionText}";
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}