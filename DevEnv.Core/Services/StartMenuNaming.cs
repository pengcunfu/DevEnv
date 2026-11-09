using System.IO;

namespace DevEnv.Core.Services;

/// <summary>开始菜单快捷方式的文件名清理与重名处理。</summary>
public static class StartMenuNaming
{
    public const int MaxBaseNameLength = 80;

    private static readonly HashSet<char> InvalidChars = new(Path.GetInvalidFileNameChars());

    /// <summary>把显示名清理成可用的文件名；非法字符、控制字符、首尾点与空格都会被去掉。</summary>
    public static string SanitizeFileName(string? displayName, int maxLength = MaxBaseNameLength)
    {
        var source = displayName ?? string.Empty;
        var cleaned = new string(source
            .Where(ch => !InvalidChars.Contains(ch) && !char.IsControl(ch))
            .ToArray())
            .Trim()
            .Trim('.', ' ');

        if (cleaned.Length > maxLength)
            cleaned = cleaned[..maxLength].TrimEnd('.', ' ');

        return cleaned.Length == 0 ? "应用" : cleaned;
    }

    /// <summary>
    /// 在已有文件名里找一个不冲突的名字：同名时依次追加「 (2)」「 (3)」，
    /// 比较大小写不敏感（Windows 文件系统默认如此）。
    /// </summary>
    public static string MakeUniqueFileName(
        string desiredBaseName,
        string extension,
        IReadOnlyCollection<string> existingFileNames)
    {
        var baseName = SanitizeFileName(desiredBaseName);
        var suffix = extension.StartsWith('.') ? extension : "." + extension;

        var taken = new HashSet<string>(
            existingFileNames
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => name is not null)
                .Cast<string>(),
            StringComparer.OrdinalIgnoreCase);

        if (!taken.Contains(baseName))
            return baseName + suffix;

        for (var index = 2; index < 10_000; index++)
        {
            var candidate = $"{baseName} ({index})";
            if (!taken.Contains(candidate))
                return candidate + suffix;
        }

        return $"{baseName} ({Guid.NewGuid():N}){suffix}";
    }
}
