using System.ComponentModel;
using System.Diagnostics;

namespace DevBox.Core.ProcessWatch.Services;

/// <summary>结束进程、打开目录等用户操作。</summary>
public static class ProcessActions
{
    public static void Kill(int pid, bool entireProcessTree = false)
    {
        using var proc = Process.GetProcessById(pid);
        proc.Kill(entireProcessTree);
    }

    public static bool TryKill(int pid, bool entireProcessTree, out string? error)
    {
        try
        {
            Kill(pid, entireProcessTree);
            error = null;
            return true;
        }
        catch (ArgumentException)
        {
            error = "进程已不存在";
            return false;
        }
        catch (Win32Exception ex)
        {
            error = $"权限不足或无法结束进程: {ex.Message}";
            return false;
        }
        catch (InvalidOperationException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryOpenFolder(string? executablePath, out string? error)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            error = "无法定位可执行文件路径";
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{executablePath}\"",
                UseShellExecute = true,
            });
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
