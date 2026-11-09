using System.Diagnostics;
using System.Net.Http;
using DevEnv.Core.Components;
using DevEnv.Core.Models;
using DevEnv.Core.Updates;

namespace DevEnv.Core.Services;

/// <summary>
/// 自研自更新状态机（检查 → 下载 zip → SHA256 校验 → 解压暂存 → 第二进程替换重启）。
/// 更新源是 GitHub Releases 的 <c>latest.json</c> 静态地址，不调用 api.github.com，避免限流。
/// 可用环境变量 <c>DEVENV_UPDATE_MANIFEST</c> 覆盖清单地址（本地联调）。
/// </summary>
public sealed class UpdateService
{
    public const string DefaultManifestUrl =
        "https://github.com/pengcunfu/DevEnv/releases/latest/download/latest.json";
    public const string ManifestEnvVar = "DEVENV_UPDATE_MANIFEST";

    private static readonly HttpClient Http = CreateHttpClient();

    private UpdateManifest? _pending;
    private string? _stagedDirectory;
    private int _downloadProgress;
    private volatile bool _downloading;
    private string? _lastError;

    public bool CanUpdate => CanSelfUpdate();

    public bool HasBackup
    {
        get
        {
            string? exe = Environment.ProcessPath;
            return exe is not null && File.Exists(exe + UpdatePaths.BackupSuffix);
        }
    }

    public UpdateStatusView GetStatus() => new()
    {
        IsInstalled = CanUpdate,
        CanUpdate = CanUpdate,
        HasBackup = HasBackup,
        CurrentVersion = AppVersionInfo.Display,
        PendingVersion = _pending?.Version,
        Downloading = _downloading,
        ReadyToApply = _pending is not null
                       && !string.IsNullOrWhiteSpace(_stagedDirectory)
                       && Directory.Exists(_stagedDirectory),
        DownloadProgress = _downloadProgress,
        Error = _lastError,
    };

    public async Task<OperationResult> CheckAsync()
    {
        try
        {
            _lastError = null;
            UpdateManifest manifest = await FetchManifestAsync().ConfigureAwait(false);

            if (!manifest.IsNewerThan(AppVersionInfo.Current))
            {
                _pending = null;
                return OperationResult.Success("已是最新版本");
            }

            _pending = manifest;
            return OperationResult.Success($"发现新版本 {manifest.Version}");
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _pending = null;
            return OperationResult.Fail(ex.Message);
        }
    }

    public OperationResult StartDownload()
    {
        if (_pending is null)
            return OperationResult.Fail("请先检查更新");
        if (_downloading)
            return OperationResult.Fail("正在下载");
        if (!CanUpdate)
            return OperationResult.Fail("当前安装不支持自动更新，请手动下载新版");

        UpdateManifest manifest = _pending;
        _downloading = true;
        _downloadProgress = 0;
        _lastError = null;

        _ = Task.Run(async () =>
        {
            try
            {
                string directory = Path.Combine(UpdatePaths.UpdatesRoot, manifest.Version);
                Directory.CreateDirectory(directory);
                string zipPath = Path.Combine(directory, "payload.zip");

                await DownloadAsync(manifest, zipPath).ConfigureAwait(false);
                _stagedDirectory = PrepareStaging(zipPath, directory);
                _downloadProgress = 100;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _stagedDirectory = null;
            }
            finally
            {
                _downloading = false;
            }
        });

        return OperationResult.Success();
    }

    public OperationResult ApplyAndRestart()
    {
        if (_pending is null)
            return OperationResult.Fail("没有待应用的更新");
        if (string.IsNullOrWhiteSpace(_stagedDirectory) || !Directory.Exists(_stagedDirectory))
            return OperationResult.Fail("更新尚未下载完成");
        if (Environment.ProcessPath is null)
            return OperationResult.Fail("无法定位当前程序路径");

        try
        {
            string arguments =
                $"--updater apply --payload \"{_stagedDirectory}\" --pid {Environment.ProcessId} " +
                $"--to {_pending.Version} --from {AppVersionInfo.Display}";

            Process.Start(new ProcessStartInfo(Environment.ProcessPath, arguments)
            {
                UseShellExecute = false,
            });

            HostCallbacks.OnBeforeExitForUpdate?.Invoke();
            return OperationResult.Success("正在应用更新并重启");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    /// <summary>手动回滚到上一版本；没有备份时返回 false。</summary>
    public bool RollbackAndRestart()
    {
        if (!HasBackup || Environment.ProcessPath is null)
            return false;

        string arguments = $"--updater rollback --pid {Environment.ProcessId}";
        Process.Start(new ProcessStartInfo(Environment.ProcessPath, arguments)
        {
            UseShellExecute = false,
        });
        HostCallbacks.OnBeforeExitForUpdate?.Invoke();
        return true;
    }

    private static async Task<UpdateManifest> FetchManifestAsync()
    {
        string url = Environment.GetEnvironmentVariable(ManifestEnvVar) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(url))
            url = DefaultManifestUrl;

        try
        {
            string json = await Http.GetStringAsync(url).ConfigureAwait(false);
            return UpdateManifestParser.Parse(json);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException($"无法解析更新清单：{url}", ex);
        }
    }

    private async Task DownloadAsync(UpdateManifest manifest, string destination)
    {
        using HttpResponseMessage response = await Http
            .GetAsync(manifest.Url, HttpCompletionOption.ResponseHeadersRead)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        long total = response.Content.Headers.ContentLength ?? manifest.Size;
        await using Stream input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        await using FileStream output = File.Create(destination);

        byte[] buffer = new byte[81920];
        long read = 0;
        int count;
        while ((count = await input.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, count)).ConfigureAwait(false);
            read += count;
            if (total > 0)
                _downloadProgress = (int)Math.Min(99, read * 99 / total);
        }

        _downloadProgress = 99;
        if (!await ComponentInstaller.MatchesSha256Async(destination, manifest.Sha256).ConfigureAwait(false))
        {
            TryDeleteFile(destination);
            throw new InvalidOperationException("更新包 SHA256 校验失败");
        }
    }

    private static string PrepareStaging(string zipPath, string directory)
    {
        string staging = Path.Combine(directory, "staging");
        try
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);

            ComponentInstaller.ExtractZipSafe(zipPath, staging);

            string exe = Path.Combine(staging, "DevEnv.exe");
            if (!File.Exists(exe))
            {
                string? nested = Directory
                    .EnumerateFiles(staging, "DevEnv.exe", SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (nested is null)
                    throw new InvalidDataException("更新包中缺少 DevEnv.exe");
                exe = nested;
            }

            return exe == Path.Combine(staging, "DevEnv.exe")
                ? staging
                : Path.GetDirectoryName(exe)!;
        }
        catch
        {
            TryDeleteDirectory(staging);
            throw;
        }
    }

    private static bool CanSelfUpdate()
    {
        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
            return false;

        string installDirectory = Path.GetDirectoryName(processPath) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(installDirectory) || !Directory.Exists(installDirectory))
            return false;

        // 开发构建 / 非单文件发布：目录里有 DevEnv.dll，不能原地替换。
        if (File.Exists(Path.Combine(installDirectory, "DevEnv.dll")))
            return false;

        // 旧 Velopack 安装：current 目录由 Update.exe 管理，不能按便携版替换。
        string directoryName = new DirectoryInfo(installDirectory).Name;
        if (string.Equals(directoryName, "current", StringComparison.OrdinalIgnoreCase))
            return false;
        string? parent = Directory.GetParent(installDirectory)?.FullName;
        if (parent is not null && File.Exists(Path.Combine(parent, "Update.exe")))
            return false;

        try
        {
            string probe = Path.Combine(installDirectory, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"DevEnv/{AppVersionInfo.Display}");
        return client;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
