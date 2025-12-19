using System.Configuration;
using System.Data;
using System.Text;
using System.Windows;

namespace DevEnv
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static App()
        {
            // 注册编码提供程序以支持GB2312等编码
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
    }

}
