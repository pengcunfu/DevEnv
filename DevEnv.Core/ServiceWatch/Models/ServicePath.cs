using System.IO;

namespace DevEnv.Core.ServiceWatch.Models;

/// <summary>解析注册表 ImagePath 中的可执行文件路径（纯函数，便于单元测试）。</summary>
public static class ServicePath
{
    /// <summary>展开环境变量与 <c>\SystemRoot</c> / <c>\??\</c> 前缀，保留引号与参数。</summary>
    public static string ExpandImagePath(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return string.Empty;

        return ResolvePath(imagePath.Trim());
    }

    /// <summary>从 ImagePath 中取出可执行文件路径：有引号取到右引号，无引号取第一个空白前的部分。</summary>
    public static string ExtractExecutable(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return string.Empty;

        var raw = imagePath.Trim();
        string candidate;

        if (raw.StartsWith('"'))
        {
            var end = raw.IndexOf('"', 1);
            candidate = end > 0 ? raw[1..end] : raw.Trim('"');
        }
        else
        {
            var expanded = ResolvePath(raw);
            if (expanded.StartsWith('"'))
            {
                var end = expanded.IndexOf('"', 1);
                candidate = end > 0 ? expanded[1..end] : expanded.Trim('"');
            }
            else
            {
                var space = expanded.IndexOfAny([' ', '\t']);
                candidate = space > 0 ? expanded[..space] : expanded;
            }
        }

        return ResolvePath(candidate);
    }

    private static string ResolvePath(string path)
    {
        var normalized = path.Trim();

        if (normalized.StartsWith(@"\??\", StringComparison.Ordinal))
            normalized = normalized[4..];

        if (normalized.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase))
        {
            var windows = Environment.GetEnvironmentVariable("SystemRoot")
                ?? Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrWhiteSpace(windows))
                normalized = Path.Combine(windows, normalized[@"\SystemRoot\".Length..]);
        }

        return Environment.ExpandEnvironmentVariables(normalized).Trim();
    }
}
