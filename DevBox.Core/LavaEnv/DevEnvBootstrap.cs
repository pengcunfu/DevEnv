using System.Text;
using DevBox.Core.LavaEnv.Models;
using DevBox.Core.LavaEnv.Services;

namespace DevBox.Core.LavaEnv;

/// <summary>
/// 熔岩环境嵌入宿主（DevEnv / 程序盒子）时的一次性初始化。
/// </summary>
public static class DevEnvBootstrap
{
    private static int _initialized;

    public static void EnsureInitialized()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
            return;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        LavaPaths.EnsureDirectories();
        AppServices.Config.Load();
    }
}
