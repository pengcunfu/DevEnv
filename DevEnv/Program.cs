using DevEnv.Core.Updates;

namespace DevEnv.Host;

/// <summary>
/// 自定义入口：先分发 <c>--updater</c> 助手模式（不创建 WPF Application、不抢单实例锁），
/// 否则解析更新重启参数并启动正常的 WPF 宿主。
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "--updater", StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode = Updater.Run(args.Skip(1).ToArray());
            return;
        }

        UpdateStartupHooks.ParseArgs(args);

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
