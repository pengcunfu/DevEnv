using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using DevEnv.Core.Updates;

namespace DevEnv.Core.Components;

/// <summary>
/// 可选组件安装器：读取 Release 里的 <c>components.json</c>，下载组件 zip、
/// 校验 SHA256、安全解压到 <see cref="ComponentStore"/> 的版本目录。
/// </summary>
public sealed class ComponentInstaller
{
    public const string DefaultManifestUrl =
        "https://github.com/pengcunfu/DevEnv/releases/latest/download/components.json";
    public const string ManifestEnvVar = "DEVENV_COMPONENTS_MANIFEST";

    private static readonly HttpClient DefaultHttp = CreateHttpClient();

    private readonly ComponentStore _store;
    private readonly HttpClient _http;

    public ComponentInstaller(ComponentStore store, HttpClient? httpClient = null)
    {
        _store = store;
        _http = httpClient ?? DefaultHttp;
    }

    public ComponentStore Store => _store;

    /// <summary>拉取组件清单；地址可用 <c>DEVENV_COMPONENTS_MANIFEST</c> 覆盖（本地联调）。</summary>
    public async Task<ComponentManifest> GetManifestAsync(CancellationToken cancellationToken = default)
    {
        string? url = Environment.GetEnvironmentVariable(ManifestEnvVar);
        if (string.IsNullOrWhiteSpace(url))
            url = DefaultManifestUrl;

        try
        {
            string json = await _http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            return ComponentManifest.Parse(json);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException($"无法获取组件清单：{url}", ex);
        }
    }

    /// <summary>下载并安装一个组件。已安装同一版本时直接返回。</summary>
    public async Task InstallAsync(
        string id,
        IProgress<ComponentProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ComponentDefinition definition = ComponentCatalog.Find(id)
            ?? throw new InvalidOperationException($"未知组件：{id}");

        ComponentManifest manifest = await GetManifestAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(manifest.AppVersion)
            && !AppVersions.IsCompatible(manifest.AppVersion, AppVersionInfo.Current))
        {
            throw new InvalidOperationException(
                $"组件包要求程序版本 {manifest.AppVersion}，当前为 {AppVersionInfo.Display}。请先更新程序。");
        }

        ComponentEntry entry = manifest.Find(definition.Id)
            ?? throw new InvalidOperationException($"组件清单中没有 {definition.DisplayName}");

        if (string.Equals(_store.GetInstalledVersion(definition.Id), entry.Version, StringComparison.OrdinalIgnoreCase)
            && _store.IsInstalled(definition.Id))
        {
            progress?.Report(new ComponentProgress(100, $"{definition.DisplayName} 已安装"));
            return;
        }

        progress?.Report(ComponentProgress.Indeterminate($"正在下载 {definition.DisplayName}…"));
        string cacheDir = _store.GetDownloadsDirectory(definition.Id);
        string zipPath = Path.Combine(cacheDir, $"{entry.Version}.zip");
        await EnsureZipAsync(entry, zipPath, progress, cancellationToken).ConfigureAwait(false);

        string finalDirectory = _store.ExpectedDirectory(definition.Id, entry.Version);
        string stagingDirectory = finalDirectory + ".staging";
        progress?.Report(new ComponentProgress(95, $"正在解压 {definition.DisplayName}…"));

        try
        {
            DeleteDirectory(stagingDirectory);
            ExtractZipSafe(zipPath, stagingDirectory);
            EnsureKeyFiles(definition, stagingDirectory);

            Directory.CreateDirectory(Path.GetDirectoryName(finalDirectory)!);
            DeleteDirectory(finalDirectory);
            Directory.Move(stagingDirectory, finalDirectory);
        }
        catch
        {
            TryDeleteDirectory(stagingDirectory);
            throw;
        }

        _store.RecordInstall(definition.Id, entry.Version, entry.Sha256, finalDirectory);
        ComponentLoader.Register(finalDirectory);
        progress?.Report(new ComponentProgress(100, $"{definition.DisplayName} 安装完成"));
    }

    /// <summary>删除组件文件与安装记录；文件被占用时返回 false 并保留记录。</summary>
    public bool Remove(string id, out string? error)
    {
        error = null;
        ComponentDefinition definition = ComponentCatalog.Find(id)
            ?? throw new InvalidOperationException($"未知组件：{id}");

        InstalledComponent? record = _store.GetRecord(definition.Id);
        if (record is not null)
        {
            string directory = _store.ExpectedDirectory(definition.Id, record.Version);
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch (Exception ex)
            {
                error = $"组件文件正在使用中，请先关闭相关预览/识别窗口后重试：{ex.Message}";
                return false;
            }

            if (Directory.Exists(directory))
            {
                error = "组件文件正在使用中，请重启程序后再删除。";
                return false;
            }
        }

        _store.Remove(definition.Id);
        return true;
    }

    private async Task EnsureZipAsync(
        ComponentEntry entry,
        string zipPath,
        IProgress<ComponentProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (File.Exists(zipPath)
            && await MatchesSha256Async(zipPath, entry.Sha256, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        TryDeleteFile(zipPath);
        Exception? lastError = null;

        foreach (string url in entry.Urls)
        {
            try
            {
                progress?.Report(ComponentProgress.Indeterminate($"正在从 {new Uri(url).Host} 下载…"));
                await DownloadFileAsync(url, zipPath, entry.Size, progress, cancellationToken).ConfigureAwait(false);

                if (!await MatchesSha256Async(zipPath, entry.Sha256, cancellationToken).ConfigureAwait(false))
                    throw new InvalidOperationException("SHA256 校验失败");

                return;
            }
            catch (OperationCanceledException)
            {
                TryDeleteFile(zipPath);
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                TryDeleteFile(zipPath);
            }
        }

        throw new InvalidOperationException(
            $"下载组件失败：{lastError?.Message ?? "没有可用下载地址"}",
            lastError);
    }

    private async Task DownloadFileAsync(
        string url,
        string destination,
        long expectedSize,
        IProgress<ComponentProgress>? progress,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        long total = response.Content.Headers.ContentLength ?? expectedSize;
        await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using FileStream output = File.Create(destination);

        byte[] buffer = new byte[81920];
        long read = 0;
        int count;
        while ((count = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
            read += count;
            if (total > 0)
                progress?.Report(new ComponentProgress((int)Math.Min(94, read * 90 / total), "正在下载…"));
        }
    }

    public static async Task<bool> MatchesSha256Async(
        string filePath,
        string expectedSha256,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256))
            return true;

        await using FileStream stream = File.OpenRead(filePath);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        string actual = Convert.ToHexString(hash).ToLowerInvariant();
        return string.Equals(actual, expectedSha256.Trim().ToLowerInvariant(), StringComparison.Ordinal);
    }

    /// <summary>解压 zip，并拒绝任何逃逸到目标目录之外的条目（zip-slip）。</summary>
    public static void ExtractZipSafe(string zipPath, string destination)
    {
        Directory.CreateDirectory(destination);
        string root = Path.GetFullPath(destination);
        string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                string directory = Path.GetFullPath(Path.Combine(root, entry.FullName));
                if (!directory.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(directory, root, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"组件包包含非法路径：{entry.FullName}");
                }
                Directory.CreateDirectory(directory);
                continue;
            }

            string fullPath = Path.GetFullPath(Path.Combine(root, entry.FullName));
            if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"组件包包含非法路径：{entry.FullName}");

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            entry.ExtractToFile(fullPath, overwrite: true);
        }
    }

    private static void EnsureKeyFiles(ComponentDefinition definition, string directory)
    {
        foreach (string file in definition.KeyFiles)
        {
            if (!File.Exists(Path.Combine(directory, file)))
                throw new InvalidDataException($"组件包缺少文件：{file}");
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"DevEnv/{AppVersionInfo.Display}");
        return client;
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // 文件被占用时由调用方提示；重试留给下次安装。
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
}
