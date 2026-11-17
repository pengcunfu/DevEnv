namespace DevEnv.Core.Services;

/// <summary>启动项命令的解析与文件状态判断。纯函数，便于单测。</summary>
public static class StartupCommandParser
{
    /// <summary>
    /// 取命令行的第一个可执行文件：展开环境变量，处理引号与参数。
    /// 例如 <c>"C:\Program Files\App\app.exe" --startup</c> 返回完整路径。
    /// </summary>
    public static string ExtractExecutablePath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return string.Empty;

        var text = Environment.ExpandEnvironmentVariables(command.Trim());
        if (text.Length == 0)
            return string.Empty;

        string token;
        if (text[0] == '"')
        {
            var end = text.IndexOf('"', 1);
            token = end > 0 ? text[1..end] : text.Trim('"');
        }
        else
        {
            var index = 0;
            while (index < text.Length && !char.IsWhiteSpace(text[index]))
                index++;
            token = text[..index];
        }

        return token.Trim().Trim('"', '\'');
    }

    /// <summary>是否只有命令名（如 <c>rundll32.exe</c>），没有目录，无法可靠定位文件。</summary>
    public static bool IsBareCommandName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return !path.Contains('\\')
            && !path.Contains('/')
            && !Path.IsPathRooted(path);
    }

    /// <summary>判断可执行文件的状态；只有命令名时标记为无法定位。</summary>
    public static StartupFileState GetFileState(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return StartupFileState.Unknown;
        if (IsBareCommandName(path))
            return StartupFileState.Unresolved;

        try
        {
            return File.Exists(path) ? StartupFileState.Exists : StartupFileState.Missing;
        }
        catch
        {
            return StartupFileState.Unknown;
        }
    }
}

/// <summary>启动项可执行文件的定位结果。</summary>
public enum StartupFileState
{
    /// <summary>无法判断。</summary>
    Unknown,

    /// <summary>文件存在。</summary>
    Exists,

    /// <summary>文件不存在。</summary>
    Missing,

    /// <summary>只有命令名，没有目录，无法定位。</summary>
    Unresolved,
}
