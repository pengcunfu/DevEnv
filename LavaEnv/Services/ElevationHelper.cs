using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace DevEnv.Services;

public static class ElevationHelper
{
    public const string EditHostsArgument = "--edit-hosts";

    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

  /// <summary>以管理员身份启动当前程序并传入参数。用户取消 UAC 时返回 false。</summary>
    public static bool TryRunElevated(string arguments)
    {
        var exePath = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(exePath))
            return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas",
            });
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return false;
        }
    }
}
