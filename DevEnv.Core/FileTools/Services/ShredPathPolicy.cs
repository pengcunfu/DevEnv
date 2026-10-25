namespace DevEnv.Core.FileTools.Services;

/// <summary>
/// 粉碎路径的安全边界：拒绝磁盘根目录、系统关键目录、符号链接/联接点，
/// 以及包含当前运行程序的目录——这些一旦粉碎就不可恢复。
/// </summary>
public static class ShredPathPolicy
{
    /// <summary>校验通过返回 null，否则返回给用户看的中文原因。</summary>
    public static string? Validate(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "请选择要粉碎的文件或文件夹";

        var trimmed = path.Trim().Trim('"');
        if (!Path.IsPathFullyQualified(trimmed))
            return "请使用完整的绝对路径";

        string full;
        try
        {
            full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(trimmed));
        }
        catch (Exception ex)
        {
            return $"路径无效：{ex.Message}";
        }

        bool isDirectory;
        try
        {
            if (File.Exists(full))
                isDirectory = false;
            else if (Directory.Exists(full))
                isDirectory = true;
            else
                return "路径不存在";
        }
        catch (Exception ex)
        {
            return $"无法访问路径：{ex.Message}";
        }

        if (IsDriveRoot(full))
            return "不能粉碎磁盘根目录";

        try
        {
            var attributes = File.GetAttributes(full);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                return "不支持粉碎符号链接或目录联接";
        }
        catch (Exception ex)
        {
            return $"无法读取路径属性：{ex.Message}";
        }

        if (isDirectory)
        {
            if (IsProtectedDirectory(full))
                return "不能粉碎 Windows、Program Files、用户目录等系统关键目录";
            if (IsSameOrUnder(AppContext.BaseDirectory, full))
                return "不能粉碎包含当前程序的目录";
        }

        return null;
    }

    /// <summary>路径本身是否为磁盘 / UNC 根目录。</summary>
    public static bool IsDriveRoot(string fullPath)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(fullPath);
        return Path.GetDirectoryName(trimmed) is null;
    }

    /// <summary>
    /// candidate 是否等于 directory 或位于其下。
    /// 两边都会做完整路径规范化，比较不区分大小写（Windows 语义）。
    /// </summary>
    public static bool IsSameOrUnder(string candidate, string directory)
    {
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

    private static bool IsProtectedDirectory(string fullPath)
    {
        foreach (var directory in GetProtectedDirectories())
        {
            if (fullPath.Equals(Path.TrimEndingDirectorySeparator(directory), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> GetProtectedDirectories()
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
                yield return path;
        }
    }
}
