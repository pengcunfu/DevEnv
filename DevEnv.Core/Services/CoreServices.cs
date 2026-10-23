using DevEnv.Core.LavaEnv;
using DevEnv.Core.LavaEnv.Services;

namespace DevEnv.Core.Services;

/// <summary>
/// 应用服务定位器。取代原先的 ASP.NET DI 容器：所有服务都是进程内单例，
/// WPF 视图模型直接 <c>CoreServices.Xxx.Method()</c> 调用。
/// </summary>
public static class CoreServices
{
    private static readonly object Gate = new();
    private static bool _initialized;

    public static DataStore Data { get; } = new();
    public static ConfigStore Config { get; } = new();

    public static ShortcutService Shortcuts { get; } = new(Data);
    public static LavaService Lava { get; } = new();
    public static DevEnvService DevEnvServices { get; } = new();
    public static ProcessService Processes { get; } = new();
    public static NetworkService Network { get; } = new();
    public static ClipboardService Clipboard { get; } = new();
    public static UtilityService Utility { get; } = new();
    public static StartupService Startup { get; } = new();
    public static TranslatorService Translator { get; } = new();
    public static SettingsService Settings { get; } = new(Config);
    public static UpdateService Update { get; } = new();
    public static ContextMenuService ContextMenu { get; } = new();

    /// <summary>
    /// 进程启动时调用一次：初始化熔岩模块（编码 / 目录 / 配置）并开启进程监控。
    /// 原先这段由 <c>MapLavaEndpoints</c> 在建应用时触发。
    /// </summary>
    public static void Initialize()
    {
        lock (Gate)
        {
            if (_initialized)
                return;
            _initialized = true;
        }

        DevEnvBootstrap.EnsureInitialized();
        DevEnv.Core.LavaEnv.Services.AppServices.ProcessManager.StartMonitoring();
    }
}