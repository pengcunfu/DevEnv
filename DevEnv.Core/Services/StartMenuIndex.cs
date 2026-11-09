using System.IO;

namespace DevEnv.Core.Services;

/// <summary>一个开始菜单 Programs 根目录；用户目录排在公共目录之后，用于覆盖同名条目。</summary>
public sealed record StartMenuRoot(string ProgramsPath, bool IsUserRoot);

/// <summary>
/// 开始菜单索引的纯文件系统部分：枚举、跨根目录合并、分组与隐藏判定。
/// 不解析 .lnk 目标、不碰图标，方便单元测试。
/// </summary>
public static class StartMenuIndex
{
    private const int MaxDepth = 8;

    /// <summary>
    /// 读取所有根目录下的 .lnk / .url。后面的根目录覆盖前面同相对路径的条目
    /// （公共目录在前、用户目录在后），并跳过受管目录与指定的一级目录。
    /// </summary>
    public static IReadOnlyList<StartMenuItem> Build(
        IReadOnlyList<StartMenuRoot> roots,
        string managedFolderName,
        IReadOnlyCollection<string>? excludedFirstLevelFolders = null)
    {
        var excluded = new HashSet<string>(
            excludedFirstLevelFolders ?? [],
            StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(managedFolderName))
            excluded.Add(managedFolderName);

        var merged = new Dictionary<string, StartMenuItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root.ProgramsPath) || !Directory.Exists(root.ProgramsPath))
                continue;

            foreach (var item in Enumerate(root.ProgramsPath, excluded))
                merged[item.RelativePath] = item;
        }

        return merged.Values
            .OrderBy(item => item.FirstLevelFolder, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<StartMenuItem> Enumerate(string programsPath, HashSet<string> excluded)
    {
        foreach (var file in EnumerateFiles(programsPath, 0, excluded))
        {
            var relative = Path.GetRelativePath(programsPath, file);
            var segments = relative.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                continue;

            var folders = segments[..^1];
            var firstLevel = folders.Length > 0 ? folders[0] : string.Empty;
            var subFolder = folders.Length > 1
                ? string.Join(Path.DirectorySeparatorChar, folders.Skip(1))
                : string.Empty;
            var extension = Path.GetExtension(file);

            yield return new StartMenuItem
            {
                Name = Path.GetFileNameWithoutExtension(file),
                LinkPath = file,
                RelativePath = relative,
                FirstLevelFolder = firstLevel,
                SubFolder = subFolder,
                Kind = extension.Equals(".url", StringComparison.OrdinalIgnoreCase)
                    ? StartMenuLinkKind.InternetShortcut
                    : StartMenuLinkKind.Shortcut,
            };
        }
    }

    private static IEnumerable<string> EnumerateFiles(
        string directory,
        int depth,
        HashSet<string> excluded)
    {
        if (depth > MaxDepth)
            yield break;

        string[] files;
        try
        {
            files = Directory.GetFiles(directory);
        }
        catch
        {
            yield break;
        }

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file);
            if (extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".url", StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }

        string[] directories;
        try
        {
            directories = Directory.GetDirectories(directory);
        }
        catch
        {
            yield break;
        }

        foreach (var child in directories)
        {
            // 一级目录里跳过受管目录（我们创建的快捷方式）与 Startup（启动项不是应用入口）
            if (depth == 0 && excluded.Contains(Path.GetFileName(child)))
                continue;

            try
            {
                // 不跟随符号链接 / 目录联接，避免循环与越界
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                    continue;
            }
            catch
            {
                continue;
            }

            foreach (var file in EnumerateFiles(child, depth + 1, excluded))
                yield return file;
        }
    }

    /// <summary>读取 .url 文件里的 URL=。文件不存在、没有该键或编码异常时返回 false。</summary>
    public static bool TryReadInternetShortcut(string path, out string url)
    {
        url = string.Empty;
        try
        {
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                var separator = line.IndexOf('=');
                if (separator <= 0)
                    continue;
                if (!line[..separator].Trim().Equals("URL", StringComparison.OrdinalIgnoreCase))
                    continue;

                var value = line[(separator + 1)..].Trim();
                if (value.Length == 0)
                    continue;

                url = value;
                return true;
            }
        }
        catch
        {
            // 读不到就当作未解析
        }

        return false;
    }

    /// <summary>路径比较用的归一化：完整路径、去掉尾部斜杠、小写。</summary>
    public static string NormalizePathForCompare(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var value = path.Trim().Trim('"');
        try
        {
            return Path.GetFullPath(value)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToLowerInvariant();
        }
        catch
        {
            return value.ToLowerInvariant();
        }
    }

    /// <summary>网址比较用的归一化：绝对地址、去掉尾部斜杠、小写。</summary>
    public static string NormalizeUrlForCompare(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        var value = url.Trim();
        try
        {
            return new Uri(value).AbsoluteUri.TrimEnd('/').ToLowerInvariant();
        }
        catch
        {
            return value.TrimEnd('/').ToLowerInvariant();
        }
    }

    /// <summary>
    /// 开始菜单条目是否已经收进启动器：.url 比网址，.lnk 比解析目标或快捷方式本身的路径。
    /// </summary>
    public static bool ShouldHide(
        StartMenuItem item,
        IReadOnlySet<string> launcherPaths,
        IReadOnlySet<string> launcherUrls)
    {
        if (item.Kind == StartMenuLinkKind.InternetShortcut)
        {
            var url = NormalizeUrlForCompare(item.Url);
            return url.Length > 0 && launcherUrls.Contains(url);
        }

        var target = NormalizePathForCompare(item.TargetPath);
        if (target.Length > 0 && launcherPaths.Contains(target))
            return true;

        var link = NormalizePathForCompare(item.LinkPath);
        return link.Length > 0 && launcherPaths.Contains(link);
    }
}
