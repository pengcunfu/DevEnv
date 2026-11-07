using System.Windows.Controls;
using System.Windows.Navigation;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.Views;

/// <summary>「关于与帮助」页：产品信息、检查更新与文档入口。</summary>
public partial class AboutHelpView : UserControl
{
    public AboutHelpView()
    {
        InitializeComponent();
    }

    private void OnLinkClick(object sender, RequestNavigateEventArgs e)
    {
        HostServices.OpenUrl(e.Uri.AbsoluteUri);
        e.Handled = true;
    }

    /// <summary>窗口关闭时释放更新进度轮询。</summary>
    public void Release() => (DataContext as System.IDisposable)?.Dispose();
}
