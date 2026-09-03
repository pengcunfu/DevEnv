using System;
using Avalonia;
using ProgramBox.Utils;
using Velopack;

namespace ProgramBox
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            // Velopack 钩子必须是 Main 的第一条语句（vpk 打包校验要求）：
            // 处理首次安装 / 更新后重启 / 启动时应用已下载的更新。
            VelopackApp.Build()
                .SetArgs(args)
                .OnFirstRun(_ => StartupHooks.FirstRun = true)
                .OnRestarted(v => StartupHooks.UpdatedTo = v.ToNormalizedString())
                .Run();

            if (!SingleInstanceService.TryStartAsPrimary())
                return;

            try
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            finally
            {
                SingleInstanceService.Dispose();
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }

    /// <summary>
    /// 从 Program.Main 向 App 传递 Velopack 钩子结果（首次安装 / 更新后重启提示用）
    /// </summary>
    internal static class StartupHooks
    {
        public static bool FirstRun { get; set; }

        public static string? UpdatedTo { get; set; }
    }
}
