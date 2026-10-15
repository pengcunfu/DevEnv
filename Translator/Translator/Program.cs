using Avalonia;
using System;
using Velopack;

namespace LavaTranslator;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build()
            .SetArgs(args)
            .OnFirstRun(_ => StartupHooks.FirstRun = true)
            .OnRestarted(v => StartupHooks.UpdatedTo = v.ToNormalizedString())
            .Run();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}

/// <summary>从 Program.Main 向界面传递 Velopack 钩子结果（首次安装 / 已更新提示用）。</summary>
internal static class StartupHooks
{
    public static bool FirstRun { get; set; }
    public static string? UpdatedTo { get; set; }
}
