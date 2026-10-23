using System.Diagnostics;

namespace DevBox.Core.LavaEnv.Services;

public static class SystemTools
{
    public static void OpenEnvironmentVariables()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "rundll32.exe",
            Arguments = "sysdm.cpl,EditEnvironmentVariables",
            UseShellExecute = true,
        });
    }
}
