using System.Diagnostics;
using System.IO;
using System.Net.Http;
using DevEnv.Host.SigXor.Toolkit.Models;

namespace DevEnv.Host.SigXor.Toolkit.Voice;

public static class SpeechModelManager
{
    public const string SenseVoiceId = "sensevoice";
    public const string ModelDirName = "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17";
    public const string ModelFileName = "model.int8.onnx";
    public const string TokensFileName = "tokens.txt";
    private const string ArchiveName = ModelDirName + ".tar.bz2";
    private const string ModelDownloadUrl =
        "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/" + ArchiveName;
    private const long MinArchiveBytes = 80L * 1024 * 1024;
    private const long MinModelBytes = 1024 * 1024;
    private const long MinTokensBytes = 1024;

    public static string ModelRoot => SigXor.AppPaths.SpeechModelsDirectory;
    public static string ArchivePath => Path.Combine(ModelRoot, ArchiveName);
    public static string LegacyModelsRoot => Path.Combine(SigXor.AppPaths.LegacySigXorDataDirectory, "models");
    public static string? LegacyModelDirectory => FindModelDirectory(LegacyModelsRoot);

    public static string? ResolveModelDirectory()
    {
        string? current = FindModelDirectory(ModelRoot);
        if (current is not null) return current;
        return FindModelDirectory(LegacyModelsRoot);
    }

    public static bool IsInstalled => ResolveModelDirectory() is not null;

    public static bool IsUsingLegacy
    {
        get
        {
            if (FindModelDirectory(ModelRoot) is not null) return false;
            return FindModelDirectory(LegacyModelsRoot) is not null;
        }
    }

    public static string StatusText
    {
        get
        {
            string? dir = ResolveModelDirectory();
            if (dir is null) return "未下载";
            return dir.StartsWith(ModelRoot, StringComparison.OrdinalIgnoreCase)
                ? "已就绪（SenseVoice）"
                : "已就绪（SenseVoice，旧目录）";
        }
    }

    public static long GetSizeOnDisk()
    {
        string? dir = ResolveModelDirectory();
        return dir is null ? 0 : GetDirectorySize(dir);
    }

    public static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return $"{size:0.##} {units[unit]}";
    }

    public static async Task<bool> DownloadAsync(
        IProgress<ModelDownloadProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        if (IsInstalled)
        {
            progress?.Report(new ModelDownloadProgress(100, "模型已就绪"));
            return true;
        }

        try
        {
            Directory.CreateDirectory(ModelRoot);
            if (!IsArchiveValid(ArchivePath))
            {
                if (!await DownloadArchiveAsync(progress, cancellationToken))
                    return false;
            }
            else
            {
                progress?.Report(new ModelDownloadProgress(-1, "正在解压模型..."));
            }

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ModelDownloadProgress(-1, "正在解压模型..."));
            bool extracted = await ExtractArchiveAsync(ArchivePath, ModelRoot, cancellationToken);
            if (!extracted && ResolveModelDirectory() is null)
            {
                progress?.Report(new ModelDownloadProgress(-1, "模型解压失败"));
                return false;
            }

            if (ResolveModelDirectory() is null)
            {
                progress?.Report(new ModelDownloadProgress(-1, "未找到 model.int8.onnx 与 tokens.txt"));
                return false;
            }

            TryDeleteArchive();
            progress?.Report(new ModelDownloadProgress(100, "SenseVoice 模型已就绪"));
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            progress?.Report(new ModelDownloadProgress(-1, $"模型准备失败：{ex.Message}"));
            return false;
        }
    }

    public static async Task<bool> MigrateFromLegacyAsync(
        Action<string>? status = null,
        CancellationToken cancellationToken = default)
    {
        if (FindModelDirectory(ModelRoot) is not null)
        {
            status?.Invoke("SenseVoice 模型已在 SigXor 数据目录");
            return true;
        }

        string? legacy = FindModelDirectory(LegacyModelsRoot);
        if (legacy is null)
        {
            status?.Invoke("未找到可迁移的旧版 SigXor SenseVoice 模型");
            return false;
        }

        try
        {
            string destination = Path.Combine(ModelRoot, Path.GetFileName(legacy));
            Directory.CreateDirectory(ModelRoot);
            status?.Invoke("正在迁移 SenseVoice 模型...");
            await ModelMigration.CopyDirectoryAsync(legacy, destination, status, cancellationToken);
            if (FindModelDirectory(ModelRoot) is null)
            {
                status?.Invoke("迁移后未找到完整模型文件");
                return false;
            }
            status?.Invoke("SenseVoice 模型迁移完成");
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            status?.Invoke($"SenseVoice 模型迁移失败：{ex.Message}");
            return false;
        }
    }

    public static bool Delete(out string? error)
    {
        error = null;
        string? current = FindModelDirectory(ModelRoot);
        if (current is null)
        {
            if (FindModelDirectory(LegacyModelsRoot) is not null)
            {
                error = "当前复用的是旧版 SigXor 数据目录中的模型，未删除。请在该目录中手动清理。";
                return false;
            }
            error = "模型未下载。";
            return false;
        }

        try
        {
            Directory.Delete(current, recursive: true);
            TryDeleteArchive();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static void OpenFolder()
    {
        string dir = ResolveModelDirectory() ?? ModelRoot;
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
    }

    private static async Task<bool> DownloadArchiveAsync(
        IProgress<ModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        string temp = ArchivePath + ".download";
        try
        {
            if (File.Exists(temp)) File.Delete(temp);
            if (File.Exists(ArchivePath)) File.Delete(ArchivePath);

            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(60) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SigXor/1.2");
            using var response = await client.GetAsync(
                ModelDownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            long total = response.Content.Headers.ContentLength ?? 0;
            await using Stream http = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using (var file = File.Create(temp))
            {
                byte[] buffer = new byte[64 * 1024];
                long downloaded = 0;
                int read;
                while ((read = await http.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    downloaded += read;
                    int percent = total > 0 ? (int)(downloaded * 100 / total) : -1;
                    progress?.Report(new ModelDownloadProgress(percent, $"正在下载 SenseVoice 模型（{FormatSize(downloaded)}）"));
                }
            }

            if (!IsArchiveValid(temp))
            {
                progress?.Report(new ModelDownloadProgress(-1, "下载不完整，请检查网络后重试"));
                TryDelete(temp);
                return false;
            }

            File.Move(temp, ArchivePath, overwrite: true);
            return true;
        }
        catch (OperationCanceledException)
        {
            TryDelete(temp);
            throw;
        }
        catch (Exception ex)
        {
            TryDelete(temp);
            progress?.Report(new ModelDownloadProgress(-1, $"下载失败：{ex.Message}"));
            return false;
        }
    }

    /// <summary>SenseVoice 包为 tar.bz2：用系统 tar.exe（-xjf），不依赖 SharpCompress。</summary>
    private static async Task<bool> ExtractArchiveAsync(
        string archivePath,
        string destination,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            string tar = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System), "tar.exe");
            if (!File.Exists(tar)) tar = "tar";

            var info = new ProcessStartInfo
            {
                FileName = tar,
                Arguments = $"-xjf \"{archivePath}\" -C \"{destination}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            using Process? process = Process.Start(info);
            if (process is null) return false;
            await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindModelDirectory(string root)
    {
        if (!Directory.Exists(root)) return null;

        string direct = Path.Combine(root, ModelDirName);
        if (IsModelDirectory(direct)) return direct;

        foreach (string file in Directory.EnumerateFiles(root, ModelFileName, SearchOption.AllDirectories))
        {
            string? dir = Path.GetDirectoryName(file);
            if (dir is not null && IsModelDirectory(dir)) return dir;
        }
        return null;
    }

    private static bool IsModelDirectory(string dir)
    {
        string model = Path.Combine(dir, ModelFileName);
        string tokens = Path.Combine(dir, TokensFileName);
        return File.Exists(model)
            && new FileInfo(model).Length >= MinModelBytes
            && File.Exists(tokens)
            && new FileInfo(tokens).Length >= MinTokensBytes;
    }

    private static bool IsArchiveValid(string path) =>
        File.Exists(path) && new FileInfo(path).Length >= MinArchiveBytes;

    private static void TryDeleteArchive()
    {
        TryDelete(ArchivePath);
        TryDelete(ArchivePath + ".download");
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static long GetDirectorySize(string dir)
    {
        try
        {
            return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
        }
        catch
        {
            return 0;
        }
    }
}
