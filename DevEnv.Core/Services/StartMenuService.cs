using System.Diagnostics;
using DevEnv.Core.Models;

namespace DevEnv.Core.Services;

/// <summary>
/// Windows 开始菜单：读取用户与公共 Programs 目录，并为启动器应用维护
/// 「开始菜单\程序\燧岚开发盒子」下的托管快捷方式。
/// </summary>
public sealed class StartMenuService
{
    /// <summary>我们创建的快捷方式所在的一级目录名，同时用于把它从开始菜单页里排除。</summary>
    public const string ManagedFolderName = "燧岚开发盒子";

    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);

    private readonly object _gate = new();
    private IReadOnlyList<StartMenuItem> _items = [];
    private HashSet<string> _targets = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _loadedAtUtc = DateTime.MinValue;
    private int _iconsEnrichedCount;

    public StartMenuService()
    {
        ManagedFolderPath = Path.Combine(GetProgramsRoot(user: true), ManagedFolderName);
    }

    /// <summary>托管快捷方式目录：%APPDATA%\Microsoft\Windows\Start Menu\Programs\燧岚开发盒子。</summary>
    public string ManagedFolderPath { get; }

    /// <summary>最近一次读取 / 写入失败的原因；成功时为空。</summary>
    public string? LastError { get; private set; }

    /// <summary>开始菜单 Programs 根目录；用户目录与公共目录各一个。</summary>
    public static string GetProgramsRoot(bool user)
    {
        var startMenu = Environment.GetFolderPath(
            user ? Environment.SpecialFolder.StartMenu : Environment.SpecialFolder.CommonStartMenu);

        if (string.IsNullOrWhiteSpace(startMenu))
        {
            var appData = Environment.GetFolderPath(
                user ? Environment.SpecialFolder.ApplicationData : Environment.SpecialFolder.CommonApplicationData);
            startMenu = Path.Combine(appData, "Microsoft", "Windows", "Start Menu");
        }

        return Path.Combine(startMenu, "Programs");
    }

    private static IReadOnlyList<StartMenuRoot> DefaultRoots() =>
    [
        new(GetProgramsRoot(user: false), IsUserRoot: false),
        new(GetProgramsRoot(user: true), IsUserRoot: true),
    ];

    /// <summary>
    /// 读取开始菜单条目并补齐目标与图标。默认使用 5 分钟缓存；
    /// <paramref name="force"/> 为 true 时重新扫描（F5 / 右键刷新）。
    /// </summary>
    public IReadOnlyList<StartMenuItem> GetItems(bool force = false)
    {
        lock (_gate)
        {
            try
            {
                EnsureCacheLocked(force);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }

            return _items;
        }
    }

    private void EnsureCacheLocked(bool force)
    {
        if (!force
            && _loadedAtUtc != DateTime.MinValue
            && DateTime.UtcNow - _loadedAtUtc < CacheLifetime)
        {
            return;
        }

        var items = StartMenuIndex.Build(DefaultRoots(), ManagedFolderName, ["Startup"]);
        ResolveTargets(items);
        _items = items;
        _targets = BuildTargets(items);
        _iconsEnrichedCount = 0;
        _loadedAtUtc = DateTime.UtcNow;
        LastError = null;
    }

    /// <summary>开始菜单索引与目标路径（不含图标提取）。</summary>
    public bool IconsFullyEnriched => _iconsEnrichedCount >= _items.Count;

    /// <summary>后台分批提取图标；返回已处理到的索引（等于 Count 时表示完成）。</summary>
    public int EnrichIconsBatch(int startIndex, int batchSize)
    {
        if (batchSize <= 0)
            batchSize = 24;

        lock (_gate)
        {
            if (_items.Count == 0)
                return 0;

            var start = Math.Max(startIndex, _iconsEnrichedCount);
            if (start >= _items.Count)
                return _items.Count;

            var end = Math.Min(start + batchSize, _items.Count);
            for (var i = start; i < end; i++)
                EnrichIcon(_items[i]);

            _iconsEnrichedCount = end;
            return end;
        }
    }

    private static void ResolveTargets(IReadOnlyList<StartMenuItem> items)
    {
        using var gateway = ShellLinkGateway.Create();
        foreach (var item in items)
        {
            if (item.Kind == StartMenuLinkKind.InternetShortcut)
            {
                item.IsResolved = StartMenuIndex.TryReadInternetShortcut(item.LinkPath, out var url);
                item.TargetPath = item.IsResolved ? url : string.Empty;
                continue;
            }

            var resolved = gateway.TryRead(item.LinkPath, out var target, out _);
            item.TargetPath = resolved ? target : string.Empty;
            item.IsResolved = resolved && !string.IsNullOrWhiteSpace(target);
        }
    }

    private static void EnrichIcon(StartMenuItem item)
    {
        if (item.Kind == StartMenuLinkKind.InternetShortcut)
        {
            item.IconPath = IconHelper.EnsureAssociatedIcon(
                item.LinkPath,
                item.LinkPath + "|" + item.TargetPath) ?? string.Empty;
            return;
        }

        item.IconPath = ResolveIcon(item, item.TargetPath);
    }

    private static string ResolveIcon(StartMenuItem item, string target)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(target) && File.Exists(target))
                return IconHelper.EnsureCachedExecutableIcon(target) ?? string.Empty;
        }
        catch
        {
            // 目标不是可提取图标的文件，退回快捷方式本身的 shell 图标
        }

        return IconHelper.EnsureAssociatedIcon(
            item.LinkPath,
            item.LinkPath + "|" + (target ?? string.Empty)) ?? string.Empty;
    }

    private HashSet<string> BuildTargets(IReadOnlyList<StartMenuItem> items)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (item.Kind != StartMenuLinkKind.Shortcut)
                continue;

            var target = StartMenuIndex.NormalizePathForCompare(item.TargetPath);
            if (target.Length > 0)
                targets.Add(target);
        }

        // 托管目录不显示在开始菜单页，但它的目标同样算“已经开始菜单里有”
        try
        {
            if (!Directory.Exists(ManagedFolderPath))
                return targets;

            using var gateway = ShellLinkGateway.Create();
            foreach (var file in Directory.EnumerateFiles(ManagedFolderPath, "*.lnk"))
            {
                if (!gateway.TryRead(file, out var target, out _))
                    continue;

                var normalized = StartMenuIndex.NormalizePathForCompare(target);
                if (normalized.Length > 0)
                    targets.Add(normalized);
            }
        }
        catch
        {
            // 读不到托管目录只影响去重，不影响主流程
        }

        return targets;
    }

    /// <summary>
    /// 确保启动器应用在开始菜单里有快捷方式。
    /// 返回托管文件名；空串表示无需创建（开始菜单里已有外部快捷方式 / 源本身是快捷方式）；
    /// <c>null</c> 表示创建失败，调用方下次可以重试。
    /// </summary>
    public string? EnsureShortcut(string targetPath, string displayName, string? currentFileName = null)
    {
        if (string.IsNullOrWhiteSpace(targetPath) || !File.Exists(targetPath))
            return string.Empty;

        // 源本身就是开始菜单快捷方式时不再复制一份
        if (IsShortcutFile(targetPath))
            return string.Empty;

        var targetKey = StartMenuIndex.NormalizePathForCompare(targetPath);
        if (targetKey.Length == 0)
            return string.Empty;

        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(currentFileName))
            {
                var current = Path.Combine(ManagedFolderPath, Path.GetFileName(currentFileName));
                if (File.Exists(current))
                    return Path.GetFileName(currentFileName);
            }

            try
            {
                EnsureCacheLocked(force: false);
            }
            catch
            {
                // 索引读取失败时仍允许继续尝试创建
            }

            var managed = FindManagedShortcutLocked(targetKey);
            if (managed is not null)
                return managed;

            if (_targets.Contains(targetKey))
                return string.Empty;

            try
            {
                Directory.CreateDirectory(ManagedFolderPath);
                var existing = Directory.GetFiles(ManagedFolderPath, "*.lnk")
                    .Select(Path.GetFileName)
                    .Where(name => name is not null)
                    .Cast<string>()
                    .ToList();
                var fileName = StartMenuNaming.MakeUniqueFileName(displayName, ".lnk", existing);
                var fullPath = Path.Combine(ManagedFolderPath, fileName);

                using var gateway = ShellLinkGateway.Create();
                if (!gateway.IsAvailable)
                {
                    LastError = "Windows Script Host 不可用，无法创建开始菜单快捷方式";
                    return null;
                }

                if (!gateway.TryWrite(fullPath, targetPath, "由燧岚开发盒子创建"))
                {
                    LastError = "创建开始菜单快捷方式失败";
                    return null;
                }

                _targets.Add(targetKey);
                _loadedAtUtc = DateTime.MinValue;
                return fileName;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return null;
            }
        }
    }

    private string? FindManagedShortcutLocked(string targetKey)
    {
        try
        {
            if (!Directory.Exists(ManagedFolderPath))
                return null;

            using var gateway = ShellLinkGateway.Create();
            foreach (var file in Directory.EnumerateFiles(ManagedFolderPath, "*.lnk"))
            {
                if (gateway.TryRead(file, out var target, out _)
                    && string.Equals(
                        StartMenuIndex.NormalizePathForCompare(target),
                        targetKey,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetFileName(file);
                }
            }
        }
        catch
        {
            // 忽略读取失败
        }

        return null;
    }

    /// <summary>只删除受管目录内、目标匹配的快捷方式；外部快捷方式不会被碰。</summary>
    public void RemoveManagedShortcut(string? fileName, string? expectedTarget = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return;

        lock (_gate)
        {
            try
            {
                var fullPath = Path.Combine(ManagedFolderPath, Path.GetFileName(fileName));
                if (!IsManagedFile(fullPath))
                    return;

                if (!string.IsNullOrWhiteSpace(expectedTarget))
                {
                    using var gateway = ShellLinkGateway.Create();
                    if (gateway.TryRead(fullPath, out var target, out _))
                    {
                        var actual = StartMenuIndex.NormalizePathForCompare(target);
                        var expected = StartMenuIndex.NormalizePathForCompare(expectedTarget);
                        if (actual.Length > 0
                            && expected.Length > 0
                            && !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }
                    }
                }

                File.Delete(fullPath);
                var key = StartMenuIndex.NormalizePathForCompare(expectedTarget);
                if (key.Length > 0)
                    _targets.Remove(key);
                _loadedAtUtc = DateTime.MinValue;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
        }
    }

    /// <summary>启动器里改名后同步修改托管快捷方式的文件名；失败时保留原名。</summary>
    public string? RenameManagedShortcut(string? fileName, string displayName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return fileName;

        lock (_gate)
        {
            try
            {
                var currentPath = Path.Combine(ManagedFolderPath, Path.GetFileName(fileName));
                if (!IsManagedFile(currentPath))
                    return fileName;

                var currentName = Path.GetFileName(currentPath);
                var existing = Directory.GetFiles(ManagedFolderPath, "*.lnk")
                    .Select(Path.GetFileName)
                    .Where(name => name is not null
                        && !string.Equals(name, currentName, StringComparison.OrdinalIgnoreCase))
                    .Cast<string>()
                    .ToList();
                var newName = StartMenuNaming.MakeUniqueFileName(displayName, ".lnk", existing);
                if (string.Equals(newName, currentName, StringComparison.OrdinalIgnoreCase))
                    return currentName;

                File.Move(currentPath, Path.Combine(ManagedFolderPath, newName));
                _loadedAtUtc = DateTime.MinValue;
                return newName;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return fileName;
            }
        }
    }

    /// <summary>路径是否落在受管目录内且文件存在。</summary>
    public bool IsManagedFile(string? pathOrName)
    {
        if (string.IsNullOrWhiteSpace(pathOrName))
            return false;

        try
        {
            var fullPath = Path.IsPathRooted(pathOrName)
                ? Path.GetFullPath(pathOrName)
                : Path.GetFullPath(Path.Combine(ManagedFolderPath, Path.GetFileName(pathOrName)));
            var root = Path.GetFullPath(ManagedFolderPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                && File.Exists(fullPath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>源文件是不是 .lnk / .url。</summary>
    public static bool IsShortcutFile(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".url", StringComparison.OrdinalIgnoreCase));

    /// <summary>用 shell 打开开始菜单快捷方式。</summary>
    public OperationResult OpenLink(string? linkPath)
    {
        if (string.IsNullOrWhiteSpace(linkPath) || !File.Exists(linkPath))
            return OperationResult.Fail("快捷方式不存在");

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = linkPath,
                UseShellExecute = true,
            });
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    /// <summary>在资源管理器中定位开始菜单快捷方式。</summary>
    public OperationResult RevealLink(string? linkPath)
    {
        if (string.IsNullOrWhiteSpace(linkPath) || !File.Exists(linkPath))
            return OperationResult.Fail("快捷方式不存在");

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{linkPath}\"",
                UseShellExecute = true,
            });
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }
}
