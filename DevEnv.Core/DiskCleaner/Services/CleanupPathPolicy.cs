using System.IO;

namespace DevEnv.Core.DiskCleaner.Services;

/// <summary>
/// C 盘清理的安全边界：清理项只允许指向临时目录 / 缓存目录，
/// 拒绝磁盘根目录与系统关键目录；遍历时跳过符号链接、目录联接和当前程序所在目录。
/// </summary>
public static class CleanupPathPolicy
{
    /// <summary>校验一个清理根路径；通过返回 null，否则返回给用户看的中文原因。</summary>
    public static string? ValidateRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "清理路径为空";

        var trimmed = path.Trim().Trim('"');
        if (!Path.IsPathFullyQualified(trimmed))
            return "清理路径必须是绝对路径";

        string full;
        try
        {
            full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(trimmed));
        }
        catch (Exception ex)
        {
            return $"清理路径无效：{ex.Message}";
        }

        if (IsDriveRoot(full))
            return "不能清理磁盘根目录";

        foreach (var protectedRoot in GetProtectedRoots())
        {
            if (string.Equals(full, protectedRoot, StringComparison.OrdinalIgnoreCase))
                return "不能清理 Windows、Program Files、用户目录等系统关键目录";
        }

        // 清理项目录本身位于程序目录内：删除会破坏正在运行的程序。
        if (IsSameOrUnder(full, AppContext.BaseDirectory))
            return "不能清理当前程序所在目录";

        return null;
    }

    /// <summary>路径本身是否为磁盘 / UNC 根目录。</summary>
    public static bool IsDriveRoot(string fullPath)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(fullPath);
        return Path.GetDirectoryName(trimmed) is null;
    }

    /// <summary>candidate 是否等于 directory 或位于其下（Windows 不区分大小写）。</summary>
    public static bool IsSameOrUnder(string? candidate, string? directory)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(directory))
            return false;

        try
        {
            var candidateFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
            var directoryFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
            if (candidateFull.Equals(directoryFull, StringComparison.OrdinalIgnoreCase))
                return true;
            return candidateFull.StartsWith(
                directoryFull + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>符号链接 / 目录联接不跟随、不删除，避免清到链接指向的外部内容。</summary>
    public static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            // 读不到属性（被占用 / 权限不足）时按危险处理，直接跳过。
            return true;
        }
    }

    /// <summary>
    /// 遍历 / 删除时是否跳过该条目：程序所在目录及其子目录一律跳过；
    /// 若该目录是程序目录的上级（例如把便携版解压到了临时目录），也整棵跳过。
    /// </summary>
    public static bool ShouldSkipEntry(string path)
    {
        if (IsSameOrUnder(path, AppContext.BaseDirectory))
            return true;
        return IsSameOrUnder(AppContext.BaseDirectory, path);
    }

    private static IEnumerable<string> GetProtectedRoots()
    {
        Environment.SpecialFolder[] specialFolders =
        [
            Environment.SpecialFolder.Windows,
            Environment.SpecialFolder.System,
            Environment.SpecialFolder.SystemX86,
            Environment.SpecialFolder.ProgramFiles,
            Environment.SpecialFolder.ProgramFilesX86,
            Environment.SpecialFolder.CommonApplicationData,
            Environment.SpecialFolder.UserProfile,
        ];

        foreach (var folder in specialFolders)
        {
            var path = Environment.GetFolderPath(folder);
            if (!string.IsNullOrWhiteSpace(path))
                yield return Path.TrimEndingDirectorySeparator(path);
        }

        // 用户目录的上级（C:\Users）也不允许作为清理根。
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var usersRoot = string.IsNullOrWhiteSpace(userProfile) ? null : Path.GetDirectoryName(
            Path.TrimEndingDirectorySeparator(userProfile));
        if (!string.IsNullOrWhiteSpace(usersRoot))
            yield return usersRoot;
    }
}
