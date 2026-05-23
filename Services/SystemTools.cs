using System.Diagnostics;

namespace DevEnv.Services;

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
