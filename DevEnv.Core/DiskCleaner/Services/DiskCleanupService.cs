using System.Diagnostics;
using System.IO;
using DevEnv.Core.DiskCleaner.Models;
using DevEnv.Core.DiskCleaner.Native;

namespace DevEnv.Core.DiskCleaner.Services;

/// <summary>
/// C 盘清理：只处理固定的临时目录与缓存目录，先扫描大小，再按用户勾选删除目录内容。
/// 不跟随符号链接 / 目录联接，跳过程序自身目录，单个文件被占用不会中断整批清理。
/// </summary>
public sealed class DiskCleanupService
{
    private const string DriveRoot = @"C:\";
    private const int MaxWarnings = 200;

    /// <summary>C 盘容量。</summary>
    public DiskSpaceInfo GetDriveSpace()
    {
        try
        {
            var drive = new DriveInfo(DriveRoot);
            if (!drive.IsReady)
                return new DiskSpaceInfo();
            return new DiskSpaceInfo
            {
                Available = true,
                TotalBytes = drive.TotalSize,
                FreeBytes = drive.AvailableFreeSpace,
            };
        }
        catch
        {
            return new DiskSpaceInfo();
        }
    }

    /// <summary>内置清理项。每次调用都按当前环境重新解析路径与浏览器配置目录。</summary>
    public IReadOnlyList<CleanupTargetDefinition> GetTargets()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var temp = Path.TrimEndingDirectorySeparator(Path.GetTempPath());

        var targets = new List<CleanupTargetDefinition>
        {
            CreateTarget(
                "user-temp", "用户临时文件", "临时文件",
                "当前用户的临时目录，安装包解压、程序缓存与运行残留都在这里，可以放心清理。",
                requiresAdmin: false, defaultSelected: true, temp),
            CreateTarget(
                "windows-temp", "Windows 临时文件", "临时文件",
                "系统级临时目录，安装程序与系统更新留下的临时文件。",
                requiresAdmin: true, defaultSelected: true,
                Path.Combine(windows, "Temp")),
            CreateTarget(
                "windows-update", "Windows 更新缓存", "系统缓存",
                "已下载的更新安装包。删除后系统需要时会重新下载，当前正在安装的更新可能受影响。",
                requiresAdmin: true, defaultSelected: false,
                Path.Combine(windows, "SoftwareDistribution", "Download")),
            CreateTarget(
                "prefetch", "预读取文件", "系统缓存",
                "记录常用程序的启动信息。删除后常用程序首次启动会稍慢，之后会自动重建。",
                requiresAdmin: true, defaultSelected: false,
                Path.Combine(windows, "Prefetch")),
            CreateTarget(
                "thumbnail-cache", "缩略图缓存", "系统缓存",
                "资源管理器为图片和视频生成的缩略图缓存，删除后打开文件夹会重新生成。",
                requiresAdmin: false, defaultSelected: true,
                Path.Combine(localAppData, "Microsoft", "Windows", "Explorer"),
                searchPattern: "thumbcache_*.db"),
            CreateTarget(
                "directx-cache", "DirectX 着色器缓存", "系统缓存",
                "游戏与图形程序编译的着色器缓存，删除后首次运行会重新编译。",
                requiresAdmin: false, defaultSelected: true,
                Path.Combine(localAppData, "D3DSCache")),
            CreateTarget(
                "inet-cache", "Internet 临时文件", "系统缓存",
                "系统组件与浏览器共享的网页临时文件缓存。",
                requiresAdmin: false, defaultSelected: true,
                Path.Combine(localAppData, "Microsoft", "Windows", "INetCache")),
            CreateTarget(
                "crash-dumps", "崩溃转储与错误报告", "系统缓存",
                "程序崩溃时生成的转储文件与 Windows 错误报告队列。",
                requiresAdmin: false, defaultSelected: true,
                Path.Combine(localAppData, "CrashDumps"),
                Path.Combine(localAppData, "Microsoft", "Windows", "WER"),
                Path.Combine(programData, "Microsoft", "Windows", "WER"),
                Path.Combine(windows, "Minidump")),
        };

        AddBrowserTarget(
            targets, "edge-cache", "Edge 浏览器缓存",
            Path.Combine(localAppData, "Microsoft", "Edge", "User Data"));
        AddBrowserTarget(
            targets, "chrome-cache", "Chrome 浏览器缓存",
            Path.Combine(localAppData, "Google", "Chrome", "User Data"));

        targets.Add(new CleanupTargetDefinition
        {
            Id = "recycle-bin",
            Name = "回收站",
            Category = "回收站",
            Description = "清空 C 盘回收站。删除后无法恢复，请确认没有需要还原的文件。",
            RequiresAdministrator = false,
            DefaultSelected = true,
            IsRecycleBin = true,
        });

        return targets;
    }

    /// <summary>扫描各清理项占用的空间。</summary>
    public CleanupScanResult Scan(
        IReadOnlyList<CleanupTargetDefinition> targets,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new CleanupScanResult();

        foreach (var target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = new CleanupScanItem { Definition = target };
            if (target.IsRecycleBin)
            {
                var (ok, size, items) = RecycleBinNative.Query(DriveRoot);
                item.Accessible = ok;
                item.SizeBytes = size;
                item.FileCount = items > int.MaxValue ? int.MaxValue : (int)items;
                item.LocationCount = item.FileCount > 0 ? 1 : 0;
                if (!ok)
                    result.Warnings.Add("无法读取回收站信息");
            }
            else
            {
                foreach (var location in target.Locations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    MeasureLocation(location, item, result.Warnings, cancellationToken);
                }
            }

            result.Items.Add(item);
        }

        result.Elapsed = stopwatch.Elapsed;
        return result;
    }

    /// <summary>清理已选中的清理项。</summary>
    /// <param name="targets">要清理的项目。</param>
    /// <param name="totalBytes">扫描得到的待清理总大小，用于计算进度；传 0 时按文件数计算。</param>
    /// <param name="totalFiles">扫描得到的待清理文件总数。</param>
    public CleanupResult Clean(
        IReadOnlyList<CleanupTargetDefinition> targets,
        long totalBytes,
        int totalFiles,
        IProgress<CleanupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new CleanupResult();
        long processedBytes = 0;
        var processedFiles = 0;
        var stopwatch = Stopwatch.StartNew();

        void Report(string targetName, string currentPath, bool force = false)
        {
            if (progress is null)
                return;
            if (!force && stopwatch.ElapsedMilliseconds < 100)
                return;
            stopwatch.Restart();
            progress.Report(new CleanupProgress
            {
                TargetName = targetName,
                CurrentPath = currentPath,
                ProcessedBytes = processedBytes,
                TotalBytes = totalBytes,
                ProcessedFiles = processedFiles,
                TotalFiles = totalFiles,
            });
        }

        foreach (var target in targets)
        {
            var outcome = new CleanupTargetOutcome { Id = target.Id, Name = target.Name };
            result.Outcomes.Add(outcome);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (target.IsRecycleBin)
                {
                    CleanRecycleBin(outcome);
                    processedBytes += outcome.FreedBytes;
                    processedFiles += outcome.DeletedFiles;
                    Report(target.Name, string.Empty, force: true);
                    continue;
                }

                foreach (var location in target.Locations)
                {
                    var error = CleanupPathPolicy.ValidateRoot(location.Path);
                    if (error is not null)
                    {
                        AddWarning(result.Warnings, $"{location.Path}：{error}");
                        outcome.SkippedFiles++;
                        continue;
                    }

                    CleanLocation(location, outcome, result.Warnings,
                        (bytes, files) =>
                        {
                            processedBytes += bytes;
                            processedFiles += files;
                        },
                        () => cancellationToken.ThrowIfCancellationRequested(),
                        path => Report(target.Name, path));
                }
            }
            catch (OperationCanceledException)
            {
                result.Canceled = true;
                break;
            }

            Report(target.Name, string.Empty, force: true);
        }

        return result;
    }

    #region 扫描

    private static void MeasureLocation(
        CleanupLocation location,
        CleanupScanItem item,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var error = CleanupPathPolicy.ValidateRoot(location.Path);
        if (error is not null)
        {
            item.Accessible = false;
            AddWarning(warnings, $"{location.Path}：{error}");
            return;
        }

        if (location.SearchPattern is { Length: > 0 })
        {
            if (!Directory.Exists(location.Path))
                return;

            item.LocationCount++;
            try
            {
                foreach (var file in Directory.EnumerateFiles(
                             location.Path, location.SearchPattern, SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (CleanupPathPolicy.ShouldSkipEntry(file) || CleanupPathPolicy.IsReparsePoint(file))
                        continue;

                    var length = SafeLength(file);
                    if (length < 0)
                        continue;
                    item.SizeBytes += length;
                    item.FileCount++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                item.Accessible = false;
                AddWarning(warnings, $"{location.Path}：{ex.Message}");
            }

            return;
        }

        if (!Directory.Exists(location.Path))
            return;

        item.LocationCount++;
        if (CleanupPathPolicy.IsReparsePoint(location.Path))
        {
            item.Accessible = false;
            AddWarning(warnings, $"{location.Path}：不支持符号链接或目录联接");
            return;
        }

        var pending = new Stack<string>();
        pending.Push(location.Path);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();

            try
            {
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (CleanupPathPolicy.ShouldSkipEntry(file) || CleanupPathPolicy.IsReparsePoint(file))
                        continue;

                    var length = SafeLength(file);
                    if (length < 0)
                        continue;
                    item.SizeBytes += length;
                    item.FileCount++;
                }

                foreach (var subdirectory in Directory.EnumerateDirectories(directory))
                {
                    if (CleanupPathPolicy.ShouldSkipEntry(subdirectory)
                        || CleanupPathPolicy.IsReparsePoint(subdirectory))
                    {
                        continue;
                    }

                    pending.Push(subdirectory);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                item.Accessible = false;
                AddWarning(warnings, $"{directory}：{ex.Message}");
            }
        }
    }

    private static long SafeLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return -1;
        }
    }

    #endregion

    #region 清理

    private static void CleanLocation(
        CleanupLocation location,
        CleanupTargetOutcome outcome,
        List<string> warnings,
        Action<long, int> advance,
        Action checkCancellation,
        Action<string> report)
    {
        if (location.SearchPattern is { Length: > 0 })
        {
            if (!Directory.Exists(location.Path))
                return;

            string[] files;
            try
            {
                files = Directory.GetFiles(location.Path, location.SearchPattern, SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                AddWarning(warnings, $"{location.Path}：{ex.Message}");
                return;
            }

            foreach (var file in files)
            {
                checkCancellation();
                if (CleanupPathPolicy.ShouldSkipEntry(file) || CleanupPathPolicy.IsReparsePoint(file))
                {
                    outcome.SkippedFiles++;
                    continue;
                }

                var length = SafeLength(file);
                if (TryDeleteFile(file))
                {
                    outcome.DeletedFiles++;
                    outcome.FreedBytes += Math.Max(0, length);
                    advance(Math.Max(0, length), 1);
                }
                else
                {
                    outcome.SkippedFiles++;
                }

                report(file);
            }

            return;
        }

        if (!Directory.Exists(location.Path))
            return;

        if (CleanupPathPolicy.IsReparsePoint(location.Path))
        {
            AddWarning(warnings, $"{location.Path}：不支持符号链接或目录联接");
            return;
        }

        var pending = new Stack<string>();
        var directories = new List<string>();
        pending.Push(location.Path);

        while (pending.Count > 0)
        {
            checkCancellation();
            var directory = pending.Pop();

            string[] files;
            try
            {
                files = Directory.GetFiles(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                AddWarning(warnings, $"{directory}：{ex.Message}");
                continue;
            }

            foreach (var file in files)
            {
                checkCancellation();
                if (CleanupPathPolicy.ShouldSkipEntry(file) || CleanupPathPolicy.IsReparsePoint(file))
                {
                    outcome.SkippedFiles++;
                    continue;
                }

                var length = SafeLength(file);
                if (TryDeleteFile(file))
                {
                    outcome.DeletedFiles++;
                    outcome.FreedBytes += Math.Max(0, length);
                    advance(Math.Max(0, length), 1);
                }
                else
                {
                    outcome.SkippedFiles++;
                }

                report(file);
            }

            string[] subdirectories;
            try
            {
                subdirectories = Directory.GetDirectories(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                AddWarning(warnings, $"{directory}：{ex.Message}");
                continue;
            }

            foreach (var subdirectory in subdirectories)
            {
                if (CleanupPathPolicy.ShouldSkipEntry(subdirectory)
                    || CleanupPathPolicy.IsReparsePoint(subdirectory))
                {
                    continue;
                }

                directories.Add(subdirectory);
                pending.Push(subdirectory);
            }
        }

        // 先删文件、再自底向上删空目录；删不掉的目录（仍被占用）保留。
        foreach (var directory in directories.OrderByDescending(d => d.Length))
        {
            checkCancellation();
            try
            {
                Directory.Delete(directory, recursive: false);
                outcome.DeletedDirectories++;
            }
            catch
            {
                // 目录仍被占用或权限不足，保留即可
            }
        }
    }

    private static bool TryDeleteFile(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            File.Delete(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void CleanRecycleBin(CleanupTargetOutcome outcome)
    {
        var (ok, sizeBefore, itemsBefore) = RecycleBinNative.Query(DriveRoot);
        if (ok && itemsBefore == 0)
            return;

        var hr = RecycleBinNative.Empty(DriveRoot);
        if (hr != 0)
        {
            outcome.Error = $"清空回收站失败（0x{hr:X8}）";
            return;
        }

        outcome.RecycleBinEmptied = true;
        var (afterOk, sizeAfter, itemsAfter) = RecycleBinNative.Query(DriveRoot);
        if (afterOk && ok)
        {
            outcome.FreedBytes = Math.Max(0, sizeBefore - sizeAfter);
            var deleted = itemsBefore - itemsAfter;
            outcome.DeletedFiles = deleted > int.MaxValue ? int.MaxValue : (int)Math.Max(0, deleted);
        }
    }

    #endregion

    #region 清理项定义

    private static CleanupTargetDefinition CreateTarget(
        string id,
        string name,
        string category,
        string description,
        bool requiresAdmin,
        bool defaultSelected,
        params string[] paths)
    {
        return new CleanupTargetDefinition
        {
            Id = id,
            Name = name,
            Category = category,
            Description = description,
            RequiresAdministrator = requiresAdmin,
            DefaultSelected = defaultSelected,
            Locations = paths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => new CleanupLocation { Path = path })
                .ToList(),
        };
    }

    private static CleanupTargetDefinition CreateTarget(
        string id,
        string name,
        string category,
        string description,
        bool requiresAdmin,
        bool defaultSelected,
        string path,
        string searchPattern)
    {
        return new CleanupTargetDefinition
        {
            Id = id,
            Name = name,
            Category = category,
            Description = description,
            RequiresAdministrator = requiresAdmin,
            DefaultSelected = defaultSelected,
            Locations = [new CleanupLocation { Path = path, SearchPattern = searchPattern }],
        };
    }

    /// <summary>Edge / Chrome 的缓存分散在每个用户配置目录里，只清理缓存子目录，不动书签 / 密码 / 历史。</summary>
    private static void AddBrowserTarget(
        List<CleanupTargetDefinition> targets,
        string id,
        string name,
        string userDataPath)
    {
        if (!Directory.Exists(userDataPath))
            return;

        var locations = ChromiumCacheLocations(userDataPath).ToList();
        if (locations.Count == 0)
            return;

        targets.Add(new CleanupTargetDefinition
        {
            Id = id,
            Name = name,
            Category = "浏览器缓存",
            Description = "只清理缓存与着色器缓存，不影响书签、密码和历史记录。关闭浏览器后清理更彻底。",
            RequiresAdministrator = false,
            DefaultSelected = true,
            Locations = locations,
        });
    }

    private static IEnumerable<CleanupLocation> ChromiumCacheLocations(string userDataPath)
    {
        string[] cacheNames = ["Cache", "Code Cache", "GPUCache", "DawnCache", "GraphiteDawnCache"];

        string[] profiles;
        try
        {
            profiles = Directory.GetDirectories(userDataPath);
        }
        catch
        {
            profiles = [];
        }

        foreach (var profile in profiles)
        {
            var profileName = Path.GetFileName(profile);
            if (!profileName.Equals("Default", StringComparison.OrdinalIgnoreCase)
                && !profileName.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var cacheName in cacheNames)
            {
                var path = Path.Combine(profile, cacheName);
                if (Directory.Exists(path))
                    yield return new CleanupLocation { Path = path };
            }
        }

        var shaderCache = Path.Combine(userDataPath, "ShaderCache", "GPUCache");
        if (Directory.Exists(shaderCache))
            yield return new CleanupLocation { Path = shaderCache };
    }

    #endregion

    private static void AddWarning(List<string> warnings, string message)
    {
        if (warnings.Count < MaxWarnings && !warnings.Contains(message))
            warnings.Add(message);
    }
}
