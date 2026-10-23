using System.Text;
using DevEnv.Models;
using DevEnv.Services;

namespace DevEnv;

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
        AppPaths.EnsureDirectories();
        AppServices.Config.Load();
    }
}
