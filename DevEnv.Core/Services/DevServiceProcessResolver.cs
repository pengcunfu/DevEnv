using System.Diagnostics;
using DevEnv.Core.Models;

namespace DevEnv.Core.Services;

/// <summary>
/// 开发环境服务的进程识别：优先使用配置里的进程名；未填写时从启动命令的
/// 第一个可执行文件推断，并用工作目录缩小范围，避免把同名进程误判成服务。
/// </summary>
public static class DevServiceProcessResolver
{
    /// <summary>
    /// 共享宿主 / 脚本启动器。它们的进程名无法唯一指向某个服务，推断时直接跳过，
    /// 需要识别这类服务请在配置里显式填写「进程名」。
    /// </summary>
    private static readonly HashSet<string> SharedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "cmd", "powershell", "pwsh", "conhost", "wt", "explorer",
        "start", "call", "set", "cd", "chcp", "echo",
        "npm", "npx", "yarn", "pnpm", "corepack",
        "node", "python", "pythonw", "py", "java", "javaw", "dotnet",
        "ruby", "php", "wsl", "bash", "sh",
    };

    /// <summary>取得用于状态检测的进程名：显式配置优先，否则从启动命令推断；无法判断时返回空串。</summary>
    public static string ResolveProcessName(DevServiceConfig config)
    {
        var explicitName = NormalizeProcessName(config.ProcessName);
        return explicitName.Length > 0 ? explicitName : ExtractExecutableName(config.StartCommand);
    }

    /// <summary>配置里是否显式填写了进程名。显式填写时只按名字匹配，不限制工作目录。</summary>
    public static bool HasExplicitProcessName(DevServiceConfig config) =>
        NormalizeProcessName(config.ProcessName).Length > 0;

    /// <summary>
    /// 从启动命令里取第一个可执行文件名：去掉引号、目录与 .exe / .com 后缀。
    /// npm、node、python 等共享宿主以及批处理 / 脚本返回空串。
    /// </summary>
    public static string ExtractExecutableName(string? startCommand)
    {
        var token = FirstToken(startCommand);
        if (token.Length == 0)
            return string.Empty;

        var name = NormalizeProcessName(token);
        return name.Length == 0 || SharedHosts.Contains(name) ? string.Empty : name;
    }

    /// <summary>
    /// 判断进程是否属于配置的工作目录。工作目录为空时只按名字匹配；
    /// 读不到进程路径时默认放行，避免因权限问题让运行中的服务一直显示已停止。
    /// 结束进程时应传 <paramref name="assumeMatchWhenUnknown"/> 为 false，读不到路径就不动它。
    /// </summary>
    public static bool MatchesWorkingDirectory(
        Process process,
        string? workingDirectory,
        bool assumeMatchWhenUnknown = true)
    {
        var directory = NormalizeDirectory(workingDirectory);
        if (directory.Length == 0)
            return true;

        string? modulePath;
        try
        {
            modulePath = process.MainModule?.FileName;
        }
        catch
        {
            return assumeMatchWhenUnknown;
        }

        if (string.IsNullOrWhiteSpace(modulePath))
            return assumeMatchWhenUnknown;

        return IsPathInside(modulePath, directory);
    }

    /// <summary>判断文件路径是否位于指定目录内；目录为空时视为匹配。</summary>
    public static bool IsPathInside(string? filePath, string? directory)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;
        if (string.IsNullOrWhiteSpace(directory))
            return true;

        try
        {
            var root = NormalizeDirectory(directory);
            var path = Path.GetFullPath(filePath.Trim().Trim('"'));
            if (path.Length == 0)
                return false;

            if (path.Equals(root, StringComparison.OrdinalIgnoreCase))
                return true;

            return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string FirstToken(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return string.Empty;

        var text = command.Trim();
        if (text[0] == '"')
        {
            var end = text.IndexOf('"', 1);
            return end > 0 ? text[1..end] : text.Trim('"');
        }

        var index = 0;
        while (index < text.Length && !char.IsWhiteSpace(text[index]))
            index++;
        return text[..index];
    }

    private static string NormalizeProcessName(string? value)
    {
        var name = (value ?? string.Empty).Trim().Trim('"', '\'');
        if (name.Length == 0)
            return string.Empty;

        name = Path.GetFileName(name);
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return name[..^4];
        if (name.EndsWith(".com", StringComparison.OrdinalIgnoreCase))
            return name[..^4];

        // 批处理、PowerShell 与脚本本身不是常驻进程，无法由名字可靠识别。
        if (name.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".vbs", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return name;
    }

    private static string NormalizeDirectory(string? directory)
    {
        var value = (directory ?? string.Empty).Trim().Trim('"');
        if (value.Length == 0)
            return string.Empty;

        try
        {
            return Path.GetFullPath(value)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return string.Empty;
        }
    }
}
