using System.IO;
using System.Text;
using System.Windows;

namespace DevEnv.Host.Windows;

/// <summary>管理员态下编辑 Hosts（由 --edit-hosts 启动，绕过单实例），WPF 版替代原 HostsEditForm。</summary>
public partial class HostsEditWindow : Window
{
    private static readonly string HostsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "drivers", "etc", "hosts");

    public HostsEditWindow()
    {
        InitializeComponent();
        PathText.Text = HostsPath;

        try
        {
            EditorControl.Text = File.Exists(HostsPath) ? File.ReadAllText(HostsPath, Encoding.UTF8) : "";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "读取失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            File.WriteAllText(HostsPath, EditorControl.Text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            System.Windows.MessageBox.Show(this, "已保存", "Hosts", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}