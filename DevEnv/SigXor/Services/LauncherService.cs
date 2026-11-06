using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using DevEnv.Core.Services;

namespace DevEnv.Host.SigXor.Services;

/// <summary>
/// 启动器搜索结果。
/// </summary>
public sealed class LauncherResult
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required LauncherCategory Category { get; init; }
    public string? Description { get; init; }
    /// <summary>可执行文件/快捷方式的实际图标（WPF ImageSource）。</summary>
    public BitmapSource? Icon { get; init; }
    /// <summary>来自全局启动器（本地应用 / 文件夹 / 网站），不再提供「固定」操作。</summary>
    public bool IsShortcutEntry { get; init; }
}

public enum LauncherCategory
{
    Application,
    File,
    Command,
    Folder,
    Website,
}

/// <summary>
/// 启动器搜索服务：搜索应用程序、文件和执行命令。
/// </summary>
public sealed class LauncherService
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);

    private readonly object _cacheGate = new();
    private readonly ShortcutService? _shortcuts;
    private List<LauncherResult>? _appCache;
    private DateTime _appCacheTime = DateTime.MinValue;
    private Task? _cacheTask;
    private List<PinnedAppEntry> _pinnedApps = [];

    /// <param name="shortcuts">全局启动器的本地应用 / 文件夹 / 网站；为空时只搜索应用、文件与命令。</param>
    public LauncherService(ShortcutService? shortcuts = null)
    {
        _shortcuts = shortcuts;
        LoadPinnedApps();
    }

    /// <summary>应用索引是否已建立（预热完成后为 true）。</summary>
    public bool HasAppCache
    {
        get { lock (_cacheGate) return _appCache is not null; }
    }

    /// <summary>
    /// 启动期预热：在线程池上枚举开始菜单 / PATH 并预先提取图标。
    /// 首次打开启动器时不再于 UI 线程上同步扫描，避免界面卡住。
    /// 重复调用只会复用同一个后台任务。
    /// </summary>
    public Task WarmupAsync()
    {
        lock (_cacheGate)
        {
            if (TryGetFreshCacheLocked() is not null) return Task.CompletedTask;
            if (_cacheTask is { IsCompleted: false }) return _cacheTask;
            // 后台构建失败时保留旧索引，不让异常冒到 UI 线程
            _cacheTask = Task.Run(() =>
            {
                try { BuildAppCache(); }
                catch { /* 保留已有缓存 */ }
            });
            return _cacheTask;
        }
    }

    /// <summary>获取常用应用（搜索框为空时显示）：固定的应用在前，系统工具在后。</summary>
    public List<LauncherResult> GetPopularApps()
    {
        // 索引过期时在后台重建；本次先用现有快照，不阻塞 UI
        _ = WarmupAsync();
        var cache = GetCacheSnapshot();
        var popular = new List<LauncherResult>();

        // 1. 固定的应用优先显示；索引里已有图标时直接复用，避免每次重复提取
        foreach (var pinned in _pinnedApps)
        {
            var known = cache?.FirstOrDefault(a =>
                string.Equals(a.Path, pinned.Path, StringComparison.OrdinalIgnoreCase));
            popular.Add(new LauncherResult
            {
                Name = pinned.Name,
                Path = pinned.Path,
                Category = LauncherCategory.Application,
                Description = pinned.Path,
                Icon = known?.Icon ?? ExtractIcon(pinned.Path),
            });
        }

        // 预热尚未完成：先返回固定应用，后台继续建索引，就绪后启动器会自行刷新
        if (cache is null) return popular.Take(12).ToList();

        // 2. 补充常用系统工具
        string[] popularNames =
        [
            "cmd", "powershell", "pwsh", "taskmgr", "notepad", "calc",
            "mspaint", "regedit", "snippingtool", "explorer", "control",
            "devmgmt", "msconfig", "resmon", "mstsc", "charmap",
        ];
        foreach (string name in popularNames)
        {
            if (popular.Count >= 12) break;
            var match = cache.FirstOrDefault(a =>
                string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match is not null && !popular.Any(p =>
                string.Equals(p.Path, match.Path, StringComparison.OrdinalIgnoreCase)))
            {
                popular.Add(match);
            }
        }

        return popular.Take(12).ToList();
    }

    /// <summary>搜索启动器结果。</summary>
    public List<LauncherResult> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var results = new List<LauncherResult>();

        // 命令模式：以 > 开头
        if (query.StartsWith('>'))
        {
            string cmd = query[1..].Trim();
            if (!string.IsNullOrWhiteSpace(cmd))
            {
                results.Add(new LauncherResult
                {
                    Name = cmd,
                    Path = cmd,
                    Category = LauncherCategory.Command,
                    Description = "执行命令",
                });
            }
            return results;
        }

        // 全局启动器里的快捷方式排在前面：它们是用户手工维护的常用入口
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddShortcutResults(query, results, seen);

        foreach (var app in SearchApplications(query))
        {
            if (seen.Add(app.Path)) results.Add(app);
        }

        foreach (var file in SearchFiles(query))
        {
            if (seen.Add(file.Path)) results.Add(file);
        }

        return results;
    }

    #region 全局启动器快捷方式

    /// <summary>
    /// 把全局启动器维护的本地应用 / 文件夹 / 网站并入搜索结果。
    /// data.json 随时可能被其他入口修改，所以每次搜索都重新读取。
    /// </summary>
    private void AddShortcutResults(string query, List<LauncherResult> results, HashSet<string> seen)
    {
        if (_shortcuts is null) return;

        foreach (var item in _shortcuts.GetApps())
        {
            if (string.IsNullOrWhiteSpace(item.ExecPath)) continue;
            if (!MatchesShortcut(item.Name, item.ExecPath, "应用", query)) continue;
            if (!seen.Add(item.ExecPath)) continue;

            results.Add(new LauncherResult
            {
                Name = string.IsNullOrWhiteSpace(item.Name)
                    ? System.IO.Path.GetFileNameWithoutExtension(item.ExecPath)
                    : item.Name,
                Path = item.ExecPath,
                Category = LauncherCategory.Application,
                Description = string.IsNullOrWhiteSpace(item.Description) ? item.ExecPath : item.Description,
                Icon = LoadIconFile(item.IconPath),
                IsShortcutEntry = true,
            });
        }

        foreach (var item in _shortcuts.GetFolders())
        {
            if (string.IsNullOrWhiteSpace(item.FolderPath)) continue;
            if (!MatchesShortcut(item.Name, item.FolderPath, "文件夹", query)) continue;
            if (!seen.Add(item.FolderPath)) continue;

            results.Add(new LauncherResult
            {
                Name = string.IsNullOrWhiteSpace(item.Name)
                    ? new DirectoryInfo(item.FolderPath).Name
                    : item.Name,
                Path = item.FolderPath,
                Category = LauncherCategory.Folder,
                Description = string.IsNullOrWhiteSpace(item.Description) ? item.FolderPath : item.Description,
                Icon = LoadIconFile(item.IconPath),
                IsShortcutEntry = true,
            });
        }

        foreach (var item in _shortcuts.GetWebs())
        {
            if (string.IsNullOrWhiteSpace(item.Url)) continue;
            if (!MatchesShortcut(item.Name, item.Url, "网站", query)) continue;
            if (!seen.Add(item.Url)) continue;

            results.Add(new LauncherResult
            {
                Name = string.IsNullOrWhiteSpace(item.Name) ? item.Url : item.Name,
                Path = item.Url,
                Category = LauncherCategory.Website,
                Description = string.IsNullOrWhiteSpace(item.Description) ? item.Url : item.Description,
                Icon = LoadIconFile(item.IconPath),
                IsShortcutEntry = true,
            });
        }
    }

    /// <summary>与全局启动器一致的匹配规则：名称、目标路径 / 网址、类型名都参与。</summary>
    private static bool MatchesShortcut(string? name, string? target, string kindText, string query) =>
        (name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || (target?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || kindText.Contains(query, StringComparison.OrdinalIgnoreCase);

    /// <summary>从快捷方式缓存图标文件加载 WPF 位图（文件缺失或损坏时回退字体图标）。</summary>
    private static BitmapSource? LoadIconFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        try
        {
            using var stream = File.OpenRead(path);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.DecodePixelWidth = 32;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    /// <summary>执行选中的启动器项。</summary>
    public void Execute(LauncherResult result)
    {
        switch (result.Category)
        {
            case LauncherCategory.Application:
            case LauncherCategory.File:
                try
                {
                    Process.Start(new ProcessStartInfo(result.Path)
                    {
                        UseShellExecute = true,
                    });
                }
                catch { }
                break;

            case LauncherCategory.Folder:
                try
                {
                    Process.Start(new ProcessStartInfo("explorer.exe")
                    {
                        Arguments = $"\"{result.Path}\"",
                        UseShellExecute = true,
                    });
                }
                catch { }
                break;

            case LauncherCategory.Website:
                try
                {
                    Process.Start(new ProcessStartInfo(result.Path)
                    {
                        UseShellExecute = true,
                    });
                }
                catch { }
                break;

            case LauncherCategory.Command:
                try
                {
                    Process.Start(new ProcessStartInfo("cmd")
                    {
                        Arguments = $"/c {result.Path}",
                        UseShellExecute = false,
                        CreateNoWindow = false,
                    });
                }
                catch { }
                break;
        }
    }

    /// <summary>以管理员身份运行（触发 UAC；用户取消时静默返回）。</summary>
    public void ExecuteAsAdministrator(LauncherResult result)
    {
        if (result.Category is LauncherCategory.Folder or LauncherCategory.Website) return;

        try
        {
            ProcessStartInfo startInfo = result.Category == LauncherCategory.Command
                ? new ProcessStartInfo("cmd")
                {
                    Arguments = $"/c {result.Path}",
                    UseShellExecute = true,
                    Verb = "runas",
                }
                : new ProcessStartInfo(result.Path)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                };
            Process.Start(startInfo);
        }
        catch
        {
            // 用户取消 UAC（Win32Exception 1223）或路径失效：静默忽略
        }
    }

    /// <summary>在资源管理器中选中该文件或快捷方式。</summary>
    public void Reveal(LauncherResult result)
    {
        if (result.Category is LauncherCategory.Command or LauncherCategory.Website) return;
        if (!File.Exists(result.Path) && !Directory.Exists(result.Path)) return;

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe")
            {
                Arguments = $"/select,\"{result.Path}\"",
                UseShellExecute = true,
            });
        }
        catch { }
    }

    /// <summary>当前项是否可以固定到快捷应用（应用结果或搜索到的可执行文件）。</summary>
    public bool CanPin(LauncherResult result) =>
        !result.IsShortcutEntry
        && result.Category is LauncherCategory.Application or LauncherCategory.File
        && !IsPinned(result.Path)
        && IsExecutablePath(result.Path);

    /// <summary>是否是可执行/可固定的程序类型（exe、快捷方式、msc、bat、cmd）。</summary>
    public static bool IsExecutablePath(string path)
    {
        string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return ext is ".exe" or ".lnk" or ".msc" or ".bat" or ".cmd";
    }

    /// <summary>固定一个应用（拖拽或手动添加）。</summary>
    public void AddPinnedApp(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        // 去重
        if (_pinnedApps.Any(p => string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase))) return;
        string name = System.IO.Path.GetFileNameWithoutExtension(path);
        _pinnedApps.Add(new PinnedAppEntry { Name = name, Path = path });
        SavePinnedApps();
    }

    /// <summary>取消固定。</summary>
    public void RemovePinnedApp(string path)
    {
        _pinnedApps.RemoveAll(p => string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase));
        SavePinnedApps();
    }

    /// <summary>检查是否已固定。</summary>
    public bool IsPinned(string path) =>
        _pinnedApps.Any(p => string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase));

    #region 固定应用持久化

    private void LoadPinnedApps()
    {
        try
        {
            if (!File.Exists(AppPaths.PinnedAppsPath)) return;
            string json = File.ReadAllText(AppPaths.PinnedAppsPath);
            _pinnedApps = JsonSerializer.Deserialize<List<PinnedAppEntry>>(json) ?? [];
        }
        catch
        {
            _pinnedApps = [];
        }
    }

    private void SavePinnedApps()
    {
        try
        {
            AppPaths.EnsureCreated();
            string json = JsonSerializer.Serialize(_pinnedApps, new JsonSerializerOptions { WriteIndented = true });
            string temp = AppPaths.PinnedAppsPath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, AppPaths.PinnedAppsPath, overwrite: true);
        }
        catch { }
    }

    #endregion

    #region 应用缓存

    private List<LauncherResult> SearchApplications(string query)
    {
        // 后台刷新索引；本次先用现有快照，绝不在 UI 线程上等待扫描完成
        _ = WarmupAsync();
        var cache = GetCacheSnapshot() ?? [];
        string lowerQuery = query.ToLowerInvariant();
        return cache
            .Where(app => app.Name.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase)
                       || app.Path.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .ToList();
    }

    private List<LauncherResult>? GetCacheSnapshot()
    {
        lock (_cacheGate) return _appCache;
    }

    private List<LauncherResult>? TryGetFreshCacheLocked()
    {
        if (_appCache is null) return null;
        if (DateTime.Now - _appCacheTime >= CacheLifetime) return null;
        return _appCache;
    }

    /// <summary>构建应用索引（线程池执行）。图标在后台提取并 Freeze，可安全跨线程使用。</summary>
    private void BuildAppCache()
    {
        var apps = new List<LauncherResult>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Start Menu 快捷方式
        string[] startMenuPaths =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
        ];

        foreach (string basePath in startMenuPaths)
        {
            if (!Directory.Exists(basePath)) continue;
            try
            {
                foreach (string lnk in Directory.EnumerateFiles(basePath, "*.lnk", SearchOption.AllDirectories))
                {
                    string name = System.IO.Path.GetFileNameWithoutExtension(lnk);
                    if (seen.Add(lnk))
                    {
                        apps.Add(new LauncherResult
                        {
                            Name = name,
                            Path = lnk,
                            Category = LauncherCategory.Application,
                            Icon = ExtractIcon(lnk),
                        });
                    }
                }
            }
            catch { }
        }

        // PATH 中的可执行文件
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (string dir in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Directory.Exists(dir)) continue;
                try
                {
                    foreach (string ext in new[] { "*.exe", "*.msc", "*.bat", "*.cmd" })
                    {
                        foreach (string exe in Directory.EnumerateFiles(dir, ext, SearchOption.TopDirectoryOnly))
                        {
                            string name = System.IO.Path.GetFileNameWithoutExtension(exe);
                            if (seen.Add(exe))
                            {
                                apps.Add(new LauncherResult
                                {
                                    Name = name,
                                    Path = exe,
                                    Category = LauncherCategory.Application,
                                    Icon = ExtractIcon(exe),
                                });
                            }
                        }
                    }
                }
                catch { }
            }
        }

        // 补充常用系统工具（可能不在 PATH 中）
        AddSystemTool(apps, seen, "PowerShell",
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"WindowsPowerShell\v1.0\powershell.exe"));
        AddSystemTool(apps, seen, "pwsh",
            @"C:\Program Files\PowerShell\7\pwsh.exe");
        AddSystemTool(apps, seen, "任务管理器",
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "taskmgr.exe"));
        AddSystemTool(apps, seen, "记事本",
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe"));
        AddSystemTool(apps, seen, "画图",
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "mspaint.exe"));
        AddSystemTool(apps, seen, "注册表编辑器",
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "regedit.exe"));
        AddSystemTool(apps, seen, "远程桌面",
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "mstsc.exe"));
        AddSystemTool(apps, seen, "资源监视器",
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "resmon.exe"));
        AddSystemTool(apps, seen, "系统配置",
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "msconfig.exe"));
        AddSystemTool(apps, seen, "控制面板",
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "control.exe"));
        AddSystemTool(apps, seen, "设备管理器",
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "devmgmt.msc"));
        AddSystemTool(apps, seen, "计算器",
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "calc.exe"));
        AddSystemTool(apps, seen, "字符映射表",
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "charmap.exe"));

        lock (_cacheGate)
        {
            _appCache = apps;
            _appCacheTime = DateTime.Now;
        }
    }

    private static void AddSystemTool(List<LauncherResult> apps, HashSet<string> seen, string name, string path)
    {
        if (!File.Exists(path)) return;
        if (!seen.Add(path)) return;
        apps.Add(new LauncherResult
        {
            Name = name,
            Path = path,
            Category = LauncherCategory.Application,
            Description = path,
            Icon = ExtractIcon(path),
        });
    }

    #endregion

    #region 文件搜索

    private static List<LauncherResult> SearchFiles(string query)
    {
        var results = new List<LauncherResult>();
        string[] searchDirs =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ];

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string dir in searchDirs)
        {
            if (!Directory.Exists(dir)) continue;
            try
            {
                foreach (string file in Directory.EnumerateFiles(dir, $"*{query}*", SearchOption.TopDirectoryOnly))
                {
                    if (seen.Add(file))
                    {
                        results.Add(new LauncherResult
                        {
                            Name = System.IO.Path.GetFileName(file),
                            Path = file,
                            Category = LauncherCategory.File,
                        });
                    }
                    if (results.Count >= 15) return results;
                }
                foreach (string subDir in Directory.EnumerateDirectories(dir, "*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        foreach (string file in Directory.EnumerateFiles(subDir, $"*{query}*", SearchOption.TopDirectoryOnly))
                        {
                            if (seen.Add(file))
                            {
                                results.Add(new LauncherResult
                                {
                                    Name = System.IO.Path.GetFileName(file),
                                    Path = file,
                                    Category = LauncherCategory.File,
                                });
                            }
                            if (results.Count >= 15) return results;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        string downloads = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (Directory.Exists(downloads) && !searchDirs.Contains(downloads))
        {
            try
            {
                foreach (string file in Directory.EnumerateFiles(downloads, $"*{query}*", SearchOption.TopDirectoryOnly))
                {
                    if (seen.Add(file))
                    {
                        results.Add(new LauncherResult
                        {
                            Name = System.IO.Path.GetFileName(file),
                            Path = file,
                            Category = LauncherCategory.File,
                        });
                    }
                    if (results.Count >= 15) return results;
                }
            }
            catch { }
        }

        return results;
    }

    #endregion

    #region 图标提取

    /// <summary>从文件路径提取图标，转换为 WPF BitmapSource。</summary>
    private static BitmapSource? ExtractIcon(string filePath)
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(filePath);
            if (icon is null) return null;
            using var bitmap = icon.ToBitmap();
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            stream.Position = 0;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = stream;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 24;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    #endregion
}

/// <summary>固定应用的持久化条目。</summary>
internal sealed class PinnedAppEntry
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
}
