using System.Windows;
using DevBox.Host.Infrastructure;

namespace DevBox.Host.Windows;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"版本 {HostServices.VersionText}";
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}