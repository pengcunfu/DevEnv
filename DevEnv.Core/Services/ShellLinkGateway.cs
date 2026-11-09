using System.Runtime.InteropServices;

namespace DevEnv.Core.Services;

/// <summary>
/// WScript.Shell 的 .lnk 读写封装。Windows Script Host 不可用（被策略禁用 / 精简系统）时
/// 所有方法都返回 false，调用方按“无法同步”降级，不影响启动器本身。
/// </summary>
internal sealed class ShellLinkGateway : IDisposable
{
    private readonly object? _shell;

    private ShellLinkGateway(object? shell) => _shell = shell;

    public bool IsAvailable => _shell is not null;

    public static ShellLinkGateway Create()
    {
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            return new ShellLinkGateway(type is null ? null : Activator.CreateInstance(type));
        }
        catch
        {
            return new ShellLinkGateway(null);
        }
    }

    /// <summary>读取快捷方式的目标路径与参数；失败返回 false。</summary>
    public bool TryRead(string lnkPath, out string targetPath, out string arguments)
    {
        targetPath = string.Empty;
        arguments = string.Empty;
        if (_shell is null)
            return false;

        try
        {
            dynamic shortcut = ((dynamic)_shell).CreateShortcut(lnkPath);
            targetPath = (string?)shortcut.TargetPath ?? string.Empty;
            arguments = (string?)shortcut.Arguments ?? string.Empty;
            return !string.IsNullOrWhiteSpace(targetPath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>创建或覆盖一个 .lnk；失败时清理半成品文件。</summary>
    public bool TryWrite(string lnkPath, string targetPath, string description)
    {
        if (_shell is null)
            return false;

        try
        {
            dynamic shortcut = ((dynamic)_shell).CreateShortcut(lnkPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath) ?? string.Empty;
            shortcut.Description = description;
            if (targetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                shortcut.IconLocation = targetPath + ",0";
            shortcut.Save();
            return File.Exists(lnkPath);
        }
        catch
        {
            try
            {
                if (File.Exists(lnkPath))
                    File.Delete(lnkPath);
            }
            catch
            {
                // 清理失败不影响调用方继续
            }
            return false;
        }
    }

    public void Dispose()
    {
        if (_shell is null)
            return;

        try
        {
            Marshal.FinalReleaseComObject(_shell);
        }
        catch
        {
            // 进程退出 / COM 已释放
        }
    }
}
