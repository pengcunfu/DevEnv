using System.Windows;
using System.Windows.Input;

namespace DevEnv.Host.Windows;

/// <summary>独立的「关于与帮助」模态窗口：产品信息、检查更新与文档入口。</summary>
public partial class AboutHelpWindow : Window
{
    public AboutHelpWindow()
    {
        InitializeComponent();
        Closed += (_, _) => AboutHelpHost.Release();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        e.Handled = true;
        Close();
    }
}
