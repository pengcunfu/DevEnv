using System.Diagnostics;
using DevBox.Core.Models;
using DevEnv;
using DevEnv.Models;
using DevEnv.Services;
using LavaAppServices = DevEnv.Services.AppServices;

namespace DevBox.Core.Services;

/// <summary>
/// 熔岩环境：绿色版进程管理、软件下载与目录、环境扫描、镜像配置、数据目录。
/// 逻辑从 <c>LavaEndpoints</c> / <c>LavaExtraEndpoints</c> 原样平移。
/// </summary>
public sealed class LavaService
{
    #region 绿色版进程

    public IReadOnlyList<LavaProcessItem> GetProcesses()
    {
        var pm = LavaAppServices.ProcessManager;
        return pm.GetDefinitions().Select(def =>
        {
            var status = pm.GetProcessStatus(def.Name);
            return new LavaProcessItem
            {
                Name = def.Name,
                DisplayName = def.DisplayName,
                Description = def.Description,
                Status = status.Status.ToString(),
                StatusText = status.DisplayText,
                StatusColor = status.Color,
                ProcessId = status.ProcessId,
                CanStart = status.Status == ProcessState.Stopped,
                CanStop = status.Status is ProcessState.Running or ProcessState.ExternalRunning,
                NeedsInstall = status.Status == ProcessState.NotConfigured,
            };
        }).ToList();
    }

    public async Task<OperationResult> StartProcessAsync(string name)
    {
        var (ok, message) = await LavaAppServices.ProcessManager.StartProcessAsync(name);
        return ok ? OperationResult.Success(message) : OperationResult.Fail(message);
    }

    public async Task<OperationResult> StopProcessAsync(string name)
    {
        var (ok, message) = await LavaAppServices.ProcessManager.StopProcessAsync(name);
        return ok ? OperationResult.Success(message) : OperationResult.Fail(message);
    }

    #endregion

    #region 目录

    public OperationResult OpenAppsDirectory() => OpenDirectory(LavaAppServices.ProcessManager.GetAppsDirectory());

    public OperationResult OpenDataRoot()
    {
        DevEnv.Models.AppPaths.EnsureDirectories();
        return OpenDirectory(DevEnv.Models.AppPaths.Root);
    }

    public OperationResult OpenConfigFile()
    {
        DevEnv.Models.AppPaths.EnsureDirectories();
        _ = LavaAppServices.Config.Load();
        if (!File.Exists(DevEnv.Models.AppPaths.ConfigFile))
            return OperationResult.Fail("配置文件不存在");
        return StartProcess(DevEnv.Models.AppPaths.ConfigFile);
    }

    public OperationResult OpenCacheDirectory()
    {
        var dir = LavaAppServices.Config.Load().CacheDir;
        if (string.IsNullOrWhiteSpace(dir))
            dir = DevEnv.Models.AppPaths.CacheDir;
        return OpenDirectory(dir);
    }

    public OperationResult OpenEnvironmentVariables() =>
        WindowsToolRunner.TryRun("env", out var error)
            ? OperationResult.Success()
            : OperationResult.Fail(error ?? "无法打开环境变量");

    private static OperationResult OpenDirectory(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = dir,
                UseShellExecute = true,
            });
            return OperationResult.Success(dir);
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    private static OperationResult StartProcess(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            return OperationResult.Success(path);
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    #endregion

    #region 下载

    public DownloadListResult GetDownloads()
    {
        var all = LavaAppServices.DownloadHistory.GetAll();
        var stats = LavaAppServices.DownloadHistory.GetStatistics();
        return new DownloadListResult
        {
            Items = all.Select(MapDownload).ToList(),
            Stats = new DownloadStats
            {
                Total = stats.Total,
                Completed = stats.Completed,
                Failed = stats.Failed,
                Downloading = stats.Downloading,
                TotalBytes = stats.TotalBytes,
                TotalBytesText = FormatBytes(stats.TotalBytes),
            },
        };
    }

    public async Task<OperationResult> StartDownloadAsync(
        string? url,
        string? fileName,
        string softwareName = "software",
        string version = "")
    {
        if (string.IsNullOrWhiteSpace(url))
            return OperationResult.Fail("需要 url");
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = InferFileName(url, softwareName, version);

        try
        {
            var id = await LavaAppServices.Download.StartDownloadAsync(url, fileName, softwareName, version);
            return OperationResult.Success(id);
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    public void CancelDownload(string id) => LavaAppServices.Download.CancelDownload(id);

    public void ClearCompletedDownloads() => LavaAppServices.DownloadHistory.ClearCompleted();

    public void ClearFailedDownloads() => LavaAppServices.DownloadHistory.ClearFailed();

    public void ClearAllDownloads() => LavaAppServices.DownloadHistory.ClearAll();

    public OperationResult OpenDownloadedFile(string id)
    {
        var record = LavaAppServices.DownloadHistory.GetById(id);
        if (record is null)
            return OperationResult.Fail("记录不存在");
        if (string.IsNullOrWhiteSpace(record.SavePath) || !File.Exists(record.SavePath))
            return OperationResult.Fail("文件不存在");

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{record.SavePath}\"",
                UseShellExecute = true,
            });
            return OperationResult.Success(record.SavePath);
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    public IReadOnlyList<CatalogCategory> GetSoftwareCatalog()
    {
        var config = ResourceLoader.LoadBundledJson<Dictionary<string, List<SoftwareCatalogItem>>>(
            ResourcePaths.SoftwareCatalogFile);
        if (config is null)
            return [];

        return config.Select(kv => new CatalogCategory
        {
            Category = kv.Key,
            Items = kv.Value.Select(item => new CatalogItem
            {
                Name = item.Name,
                Description = item.Desc,
                Icon = item.Icon,
                Versions = item.Versions.Select(v => new CatalogVersion
                {
                    Version = v.Version,
                    Url = v.Url,
                    FileName = string.IsNullOrWhiteSpace(v.FileName)
                        ? InferFileName(v.Url, item.Name, v.Version)
                        : v.FileName,
                }).ToList(),
            }).ToList(),
        }).ToList();
    }

    #endregion

    #region 镜像

    public async Task<MirrorInfo> GetMirrorsAsync(string? tool)
    {
        var toolType = Enum.TryParse<MirrorToolType>(tool, true, out var parsed) ? parsed : MirrorToolType.Pip;
        var mirrors = LavaAppServices.MirrorConfig.GetMirrors(toolType);
        var current = await LavaAppServices.MirrorConfig.GetCurrentMirrorAsync(toolType);
        return new MirrorInfo
        {
            Tool = toolType.ToString(),
            Tools = Enum.GetValues<MirrorToolType>().Select(t => new MirrorToolOption
            {
                Key = t.ToString(),
                Label = MirrorConfigService.GetToolDisplayName(t),
            }).ToList(),
            Current = current,
            Mirrors = mirrors.Select(MapMirror).ToList(),
        };
    }

    /// <summary>镜像测速；返回 (操作结果, 测速结果)。</summary>
    public async Task<(OperationResult Operation, MirrorTestResult? Test)> TestMirrorsAsync(string? tool)
    {
        if (!Enum.TryParse<MirrorToolType>(tool, true, out var toolType))
            return (OperationResult.Fail("未知工具"), null);

        var mirrors = LavaAppServices.MirrorConfig.GetMirrors(toolType);
        var results = await LavaAppServices.MirrorConfig.TestMirrorSpeedAsync(mirrors);
        var fastest = results.FirstOrDefault(m => m.ResponseTimeMs.HasValue);
        return (OperationResult.Success(), new MirrorTestResult
        {
            Mirrors = results.Select(MapMirror).ToList(),
            Recommended = fastest is null ? null : MapMirror(fastest),
        });
    }

    public async Task<OperationResult> ConfigureMirrorAsync(string? tool, string? key)
    {
        if (!Enum.TryParse<MirrorToolType>(tool, true, out var toolType))
            return OperationResult.Fail("未知工具");

        var mirror = LavaAppServices.MirrorConfig.GetMirrors(toolType)
            .FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));
        if (mirror is null)
            return OperationResult.Fail("未知镜像");

        var (ok, message) = await LavaAppServices.MirrorConfig.ConfigureMirrorAsync(toolType, mirror);
        return ok ? OperationResult.Success(message) : OperationResult.Fail(message);
    }

    #endregion

    #region 环境扫描 / 数据目录

    public Task<List<InstalledEnvironment>> ScanAsync() =>
        LavaAppServices.EnvironmentScanner.ScanAllAsync();

    public LavaDataInfo GetDataInfo()
    {
        DevEnv.Models.AppPaths.EnsureDirectories();
        var settings = LavaAppServices.Config.Load();
        return new LavaDataInfo
        {
            Root = DevEnv.Models.AppPaths.Root,
            ConfigFile = DevEnv.Models.AppPaths.ConfigFile,
            CacheDir = string.IsNullOrWhiteSpace(settings.CacheDir) ? DevEnv.Models.AppPaths.CacheDir : settings.CacheDir,
            AppsDir = string.IsNullOrWhiteSpace(settings.AppsDir) ? DevEnv.Models.AppPaths.AppsDir : settings.AppsDir,
            HistoryFile = DevEnv.Models.AppPaths.DownloadHistoryFile,
            Settings = new LavaSettingsInfo
            {
                AutoExtractPortable = settings.AutoExtractPortable,
                MaxRetries = settings.MaxRetries,
                Timeout = settings.Timeout,
                ChunkSizeMb = settings.ChunkSizeMb,
                UseSystemProxy = settings.UseSystemProxy,
                CustomProxy = settings.CustomProxy ?? string.Empty,
            },
            About = new LavaAboutInfo
            {
                ProductName = AppInfo.ProductName,
                ProductShortName = AppInfo.ProductShortName,
                Tagline = AppInfo.ProductTagline,
                Version = AppInfo.Version,
                DocsUrl = AppInfo.DocsUrl,
                RepositoryUrl = AppInfo.RepositoryUrl,
            },
        };
    }

    #endregion

    private static DownloadItem MapDownload(DownloadRecord r) => new()
    {
        Id = r.Id,
        SoftwareName = r.SoftwareName,
        Version = r.Version,
        FileName = r.FileName,
        FilePath = r.SavePath,
        Url = r.Url,
        Status = r.Status.ToString(),
        StatusText = r.StatusText,
        Progress = r.Progress,
        SpeedText = r.SpeedText,
        SizeText = r.SizeText,
        StartTime = r.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
        EndTime = r.EndTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
        ErrorMessage = r.ErrorMessage ?? string.Empty,
        CanOpen = !string.IsNullOrWhiteSpace(r.SavePath) && File.Exists(r.SavePath),
        IsActive = r.Status is DownloadStatus.Pending or DownloadStatus.Downloading,
    };

    private static MirrorItem MapMirror(MirrorSource m) => new()
    {
        Key = m.Key,
        Name = m.Name,
        Url = m.Url,
        ResponseTimeMs = m.ResponseTimeMs,
    };

    private static string InferFileName(string url, string softwareName, string version)
    {
        if (string.IsNullOrEmpty(url))
            return $"{softwareName}_{version}.exe";
        try
        {
            var name = Path.GetFileName(new Uri(url).LocalPath);
            if (!string.IsNullOrEmpty(name) && name != "/")
                return name;
        }
        catch
        {
            // 非法 URI 时退回拼接
        }

        return $"{softwareName}_{version}{Path.GetExtension(url)}";
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        var order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}