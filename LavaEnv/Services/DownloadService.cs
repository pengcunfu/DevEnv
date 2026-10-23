using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using DevEnv.Models;
using Microsoft.Win32;


namespace DevEnv.Services
{
    public class DownloadProgressEventArgs : EventArgs
    {
        public string DownloadId { get; init; } = string.Empty;
        public int Progress { get; init; }
        public long DownloadedSize { get; init; }
        public long TotalSize { get; init; }
        public double Speed { get; init; }
        public int Eta { get; init; }
    }

    public class DownloadService
    {
        private readonly AppConfigService _configService;
        private readonly DownloadHistoryService _historyService;
        private readonly PortableInstallService _portableInstall;
        private readonly Dictionary<string, CancellationTokenSource> _activeDownloads = new();

        public event EventHandler<DownloadProgressEventArgs>? ProgressChanged;
        public event EventHandler<string>? DownloadCompleted;
        public event EventHandler<(string Id, string Error)>? DownloadFailed;
        public event EventHandler<(string Id, string Message)>? PortableInstalled;

        public DownloadService(AppConfigService configService, DownloadHistoryService historyService, PortableInstallService portableInstall)
        {
            _configService = configService;
            _historyService = historyService;
            _portableInstall = portableInstall;
        }

        public async Task<string> StartDownloadAsync(string url, string fileName, string softwareName, string version)
        {
            var settings = _configService.Load();
            var downloadDir = settings.CacheDir;
            Directory.CreateDirectory(downloadDir);

            var filePath = Path.Combine(downloadDir, fileName);
            var record = _historyService.Add(url, fileName, filePath, softwareName, version);

            var cts = new CancellationTokenSource();
            _activeDownloads[record.Id] = cts;

            _ = Task.Run(async () =>
            {
                try
                {
                    await DownloadFileAsync(record, settings, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    _historyService.Update(record.Id, r =>
                    {
                        r.Status = DownloadStatus.Cancelled;
                    });
                }
                catch (Exception ex)
                {
                    _historyService.Update(record.Id, r =>
                    {
                        r.Status = DownloadStatus.Failed;
                        r.ErrorMessage = ex.Message;
                    });
                    DownloadFailed?.Invoke(this, (record.Id, ex.Message));
                }
                finally
                {
                    _activeDownloads.Remove(record.Id);
                }
            });

            return record.Id;
        }

        public void CancelDownload(string downloadId)
        {
            if (_activeDownloads.TryGetValue(downloadId, out var cts))
            {
                cts.Cancel();
                _activeDownloads.Remove(downloadId);
            }
        }

        public bool IsDownloading(string downloadId) => _activeDownloads.ContainsKey(downloadId);

        private async Task DownloadFileAsync(DownloadRecord record, AppSettings settings, CancellationToken cancellationToken)
        {
            _historyService.Update(record.Id, r => r.Status = DownloadStatus.Downloading);

            using var handler = CreateHttpHandler(settings);
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(settings.Timeout)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"LavaEnv/{AppInfo.Version} (Windows NT 10.0; Win64; x64)");

            var existingSize = File.Exists(record.SavePath) ? new FileInfo(record.SavePath).Length : 0L;
            long totalSize = 0;
            var retries = 0;

            while (retries <= settings.MaxRetries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    try
                    {
                        using var headRequest = new HttpRequestMessage(HttpMethod.Head, record.Url);
                        using var headResponse = await client.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                        if (headResponse.IsSuccessStatusCode)
                            totalSize = headResponse.Content.Headers.ContentLength ?? 0;
                    }
                    catch
                    {
                        // 部分服务器不支持 HEAD，继续走 GET 下载
                    }

                    if (existingSize > 0 && totalSize > 0 && existingSize < totalSize)
                    {
                        // resume supported
                    }
                    else if (existingSize >= totalSize && totalSize > 0)
                    {
                        _historyService.Update(record.Id, r =>
                        {
                            r.Status = DownloadStatus.Completed;
                            r.Progress = 100;
                            r.DownloadedSize = totalSize;
                            r.TotalSize = totalSize;
                        });
                        await TryAutoExtractAsync(record, settings);
                        DownloadCompleted?.Invoke(this, record.Id);
                        return;
                    }
                    else
                    {
                        existingSize = 0;
                    }

                    using var request = new HttpRequestMessage(HttpMethod.Get, record.Url);
                    if (existingSize > 0)
                        request.Headers.Range = new RangeHeaderValue(existingSize, null);

                    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var mode = existingSize > 0 ? FileMode.Append : FileMode.Create;
                    long downloaded;
                    {
                        await using var fileStream = new FileStream(record.SavePath, mode, FileAccess.Write, FileShare.None);
                        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

                        if (totalSize == 0)
                            totalSize = response.Content.Headers.ContentLength ?? 0;

                        var buffer = new byte[settings.ChunkSizeMb * 1024 * 1024];
                        downloaded = existingSize;
                        var stopwatch = Stopwatch.StartNew();
                        var lastReport = stopwatch.Elapsed;
                        var lastDownloaded = downloaded;
                        int bytesRead;

                        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                        {
                            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                            downloaded += bytesRead;

                            if (stopwatch.Elapsed - lastReport >= TimeSpan.FromMilliseconds(500))
                            {
                                var elapsed = (stopwatch.Elapsed - lastReport).TotalSeconds;
                                var speed = elapsed > 0 ? (downloaded - lastDownloaded) / elapsed : 0;
                                var progress = totalSize > 0 ? (int)(downloaded * 100 / totalSize) : 0;
                                var eta = speed > 0 && totalSize > 0 ? (int)((totalSize - downloaded) / speed) : 0;

                                _historyService.Update(record.Id, r =>
                                {
                                    r.Progress = progress;
                                    r.DownloadedSize = downloaded;
                                    r.TotalSize = totalSize;
                                    r.Speed = speed;
                                    r.Eta = eta;
                                });

                                ProgressChanged?.Invoke(this, new DownloadProgressEventArgs
                                {
                                    DownloadId = record.Id,
                                    Progress = progress,
                                    DownloadedSize = downloaded,
                                    TotalSize = totalSize,
                                    Speed = speed,
                                    Eta = eta
                                });

                                lastReport = stopwatch.Elapsed;
                                lastDownloaded = downloaded;
                            }
                        }
                    }

                    _historyService.Update(record.Id, r =>
                    {
                        r.Status = DownloadStatus.Completed;
                        r.Progress = 100;
                        r.DownloadedSize = downloaded;
                        r.TotalSize = totalSize > 0 ? totalSize : downloaded;
                        r.Speed = 0;
                        r.Eta = 0;
                    });

                    await TryAutoExtractAsync(record, settings);
                    DownloadCompleted?.Invoke(this, record.Id);
                    return;
                }
                catch (Exception) when (retries < settings.MaxRetries)
                {
                    retries++;
                    existingSize = File.Exists(record.SavePath) ? new FileInfo(record.SavePath).Length : 0L;
                    await Task.Delay(1000 * retries, cancellationToken);
                }
            }

            throw new InvalidOperationException($"下载失败，已重试 {settings.MaxRetries} 次");
        }

        private async Task TryAutoExtractAsync(DownloadRecord record, AppSettings settings)
        {
            if (!settings.AutoExtractPortable) return;

            var path = record.SavePath;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var isPortableArchive = ext == ".zip" || ext == ".phar" || ext == ".7z" || ext == ".tgz" ||
                                    path.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
                                    SevenZipHelper.Is7zSelfExtracting(path) ||
                                    (ext == ".exe" && IsSingleFilePortable(record.SoftwareName));
            if (!isPortableArchive) return;

            var (success, message, _) = await _portableInstall.ExtractPortableAsync(
                record.SavePath, record.SoftwareName, record.Version);

            if (success)
                PortableInstalled?.Invoke(this, (record.Id, message));
        }

        private static bool IsSingleFilePortable(string softwareName)
        {
            var name = softwareName.ToLowerInvariant();
            return name is "minio" or "composer";
        }

        private static HttpClientHandler CreateHttpHandler(AppSettings settings)
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.All
            };

            if (!string.IsNullOrWhiteSpace(settings.CustomProxy))
            {
                handler.Proxy = new WebProxy(settings.CustomProxy);
                handler.UseProxy = true;
            }
            else if (settings.UseSystemProxy)
            {
                var systemProxy = GetSystemProxy();
                if (systemProxy != null)
                {
                    handler.Proxy = systemProxy;
                    handler.UseProxy = true;
                }
            }

            return handler;
        }

        private static WebProxy? GetSystemProxy()
        {
            try
            {
                var proxy = WebRequest.GetSystemWebProxy();
                proxy.Credentials = CredentialCache.DefaultCredentials;
                return proxy as WebProxy;
            }
            catch
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
                    if (key?.GetValue("ProxyEnable") is int enabled && enabled == 1)
                    {
                        var proxyServer = key.GetValue("ProxyServer") as string;
                        if (!string.IsNullOrWhiteSpace(proxyServer))
                            return new WebProxy($"http://{proxyServer}");
                    }
                }
                catch
                {
                    // ignore
                }
            }

            return null;
        }
    }
}

