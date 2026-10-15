using DevBox.Api;
using DevEnv.Services;
using Velopack;

namespace DevBox.Host;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build()
            .SetArgs(args)
            .Run();

        ApplicationConfiguration.Initialize();

        // 管理员编辑 Hosts：绕过单实例，独立小窗
        if (args.Any(a => string.Equals(a, ElevationHelper.EditHostsArgument, StringComparison.OrdinalIgnoreCase)))
        {
            Application.Run(new HostsEditForm());
            return;
        }

        if (!SingleInstance.TryStartAsPrimary())
            return;

        var api = new DevBoxApiHost();
        try
        {
            api.StartAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"无法启动本机 API（端口 {ApiConstants.ApiPort}）:\n{ex.Message}\n\n请确认端口未被占用。",
                "程序盒子",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            SingleInstance.Dispose();
            return;
        }

        try
        {
            var form = new MainForm(api);
            SingleInstance.StartActivateListener(form);
            Application.Run(form);
        }
        finally
        {
            api.DisposeAsync().AsTask().GetAwaiter().GetResult();
            SingleInstance.Dispose();
        }
    }
}
