using System.Windows;
using System.Windows.Input;

namespace DevEnv.Host.Windows;

/// <summary>设置对话框：菜单打开，复用 SettingsView，关闭时由视图卸载释放轮询。</summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        Close();
        e.Handled = true;
    }
}
