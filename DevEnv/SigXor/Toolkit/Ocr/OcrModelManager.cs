using System.IO;
using System.Net.Http;
using DevEnv.Host.SigXor.Toolkit.Models;

namespace DevEnv.Host.SigXor.Toolkit.Ocr;

public sealed record OcrModelSet(
    string DetPath,
    string ClsPath,
    string RecPath,
    string DictPath,
    bool IsV6,
    bool IsLegacy);

public static class OcrModelManager
{
    public const string V6DetFileName = "PP-OCRv6_det_small.onnx";
    public const string V6RecFileName = "PP-OCRv6_rec_small.onnx";
    public const string V6DictFileName = "ppocrv6_dict.txt";
    public const string V5DetFileName = "ch_PP-OCRv5_det_mobile.onnx";
    public const string V5RecFileName = "ch_PP-OCRv5_rec_mobile.onnx";
    public const string V5DictFileName = "ppocrv5_dict.txt";
    public const string ClsFileName = "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx";

    private const string BundledClassifierPath = "models/v5/" + ClsFileName;
    private const string ModelScopeBase =
        "https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.9.2/";
    private const string V6DetUrl = ModelScopeBase + "onnx/PP-OCRv6/det/" + V6DetFileName;
    private const string V6RecUrl = ModelScopeBase + "onnx/PP-OCRv6/rec/" + V6RecFileName;
    private const string V6DictUrl =
        ModelScopeBase + "paddle/PP-OCRv6/rec/PP-OCRv6_rec_small/" + V6DictFileName;
    private const string V5DetUrl = ModelScopeBase + "onnx/PP-OCRv5/det/" + V5DetFileName;
    private const string V5RecUrl = ModelScopeBase + "onnx/PP-OCRv5/rec/" + V5RecFileName;
    private const string V5DictUrl =
        ModelScopeBase + "paddle/PP-OCRv5/rec/ch_PP-OCRv5_rec_mobile/" + V5DictFileName;

    private const long MinV6DetBytes = 2L * 1024 * 1024;
    private const long MinV6RecBytes = 10L * 1024 * 1024;
    private const long MinV5DetBytes = 2L * 1024 * 1024;
    private const long MinV5RecBytes = 8L * 1024 * 1024;
    private const long MinDictBytes = 30 * 1024;
    private const long MinClassifierBytes = 512 * 1024;

    public static string ModelsRoot => SigXor.AppPaths.OcrModelsDirectory;
    public static string V6Directory => Path.Combine(ModelsRoot, "v6");
    public static string V5Directory => Path.Combine(ModelsRoot, "v5");
    public static string ClsDirectory => Path.Combine(ModelsRoot, "cls");
    public static string ClsPath => Path.Combine(ClsDirectory, ClsFileName);

    public static string LegacyOcrRoot =>
        Path.Combine(SigXor.AppPaths.LegacySigXorDataDirectory, "models", "ocr");

    public static bool IsReady => GetActiveModelSet() is not null;

    public static bool IsUsingLegacy
    {
        get
        {
            OcrModelSet? set = GetActiveModelSet();
            return set is not null && set.IsLegacy;
        }
    }

    public static string StatusText
    {
        get
        {
            OcrModelSet? set = GetActiveModelSet();
            if (set is null) return "未下载";
            string version = set.IsV6 ? "PP-OCRv6" : "PP-OCRv5";
            return set.IsLegacy
                ? $"已就绪（{version}，旧目录）"
                : $"已就绪（{version}）";
        }
    }

    public static OcrModelSet? GetActiveModelSet()
    {
        OcrModelSet? set = TryBuildSet(
            V6Directory, V6DetFileName, V6RecFileName, V6DictFileName, ClsPath, true,
            MinV6DetBytes, MinV6RecBytes, isLegacy: false);
        if (set is not null) return set;

        set = TryBuildSet(
            V5Directory, V5DetFileName, V5RecFileName, V5DictFileName, ClsPath, false,
            MinV5DetBytes, MinV5RecBytes, isLegacy: false);
        if (set is not null) return set;

        string legacyV6 = Path.Combine(LegacyOcrRoot, "v6");
        string legacyCls = Path.Combine(legacyV6, ClsFileName);
        set = TryBuildSet(
            legacyV6, V6DetFileName, V6RecFileName, V6DictFileName, legacyCls, true,
            MinV6DetBytes, MinV6RecBytes, isLegacy: true);
        if (set is not null) return set;

        string legacyV5 = Path.Combine(LegacyOcrRoot, "v5");
        return TryBuildSet(
            legacyV5, V5DetFileName, V5RecFileName, V5DictFileName, legacyCls, false,
            MinV5DetBytes, MinV5RecBytes, isLegacy: true);
    }

    public static long GetSizeOnDisk()
    {
        OcrModelSet? set = GetActiveModelSet();
        if (set is null) return 0;
        long size = 0;
        foreach (string path in new[] { set.DetPath, set.ClsPath, set.RecPath, set.DictPath })
        {
            try { size += new FileInfo(path).Length; } catch { }
        }
        return size;
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

    public static async Task<bool> EnsureReadyAsync(
        Action<string>? status = null,
        CancellationToken cancellationToken = default)
    {
        if (GetActiveModelSet() is not null)
        {
            status?.Invoke("OCR 模型已就绪");
            return true;
        }

        try
        {
            Directory.CreateDirectory(V6Directory);
            Directory.CreateDirectory(V5Directory);
            Directory.CreateDirectory(ClsDirectory);
            CopyBundledClassifier(status);

            if (!File.Exists(ClsPath) || new FileInfo(ClsPath).Length < MinClassifierBytes)
            {
                status?.Invoke("缺少 OCR 分类器模型，请重新构建或发布程序");
                return false;
            }

            status?.Invoke("正在准备 PP-OCRv6 模型（约 30MB）...");
            bool v6Ok =
                await EnsureFileAsync(V6DetPath, V6DetUrl, MinV6DetBytes, "检测模型", status, cancellationToken)
                && await EnsureFileAsync(V6RecPath, V6RecUrl, MinV6RecBytes, "识别模型", status, cancellationToken)
                && await EnsureFileAsync(V6DictPath, V6DictUrl, MinDictBytes, "字典", status, cancellationToken);

            if (v6Ok && GetActiveModelSet() is not null)
            {
                status?.Invoke("OCR 模型就绪（PP-OCRv6）");
                return true;
            }

            status?.Invoke("PP-OCRv6 下载失败，回退 PP-OCRv5（约 16MB）...");
            bool v5Ok =
                await EnsureFileAsync(V5DetPath, V5DetUrl, MinV5DetBytes, "检测模型", status, cancellationToken)
                && await EnsureFileAsync(V5RecPath, V5RecUrl, MinV5RecBytes, "识别模型", status, cancellationToken)
                && await EnsureFileAsync(V5DictPath, V5DictUrl, MinDictBytes, "字典", status, cancellationToken);

            if (v5Ok && GetActiveModelSet() is not null)
            {
                status?.Invoke("OCR 模型就绪（PP-OCRv5）");
                return true;
            }

            status?.Invoke("OCR 模型不完整，请检查网络后重试");
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            status?.Invoke($"OCR 模型准备失败：{ex.Message}");
            return false;
        }
    }

    public static async Task<bool> MigrateFromLegacyAsync(
        Action<string>? status = null,
        CancellationToken cancellationToken = default)
    {
        if (GetActiveModelSet() is not null && !IsUsingLegacy)
        {
            status?.Invoke("OCR 模型已在 SigXor 数据目录");
            return true;
        }

        try
        {
            Directory.CreateDirectory(V6Directory);
            Directory.CreateDirectory(V5Directory);
            Directory.CreateDirectory(ClsDirectory);

            string legacyV6 = Path.Combine(LegacyOcrRoot, "v6");
            string legacyV5 = Path.Combine(LegacyOcrRoot, "v5");
            bool migrated = false;
            bool currentV6 = File.Exists(V6DetPath) && File.Exists(V6RecPath) && File.Exists(V6DictPath);
            bool currentV5 = File.Exists(V5DetPath) && File.Exists(V5RecPath) && File.Exists(V5DictPath);

            if (Directory.Exists(legacyV6) && !currentV6)
            {
                status?.Invoke("正在迁移 PP-OCRv6 模型...");
                await ModelMigration.CopyDirectoryAsync(
                    legacyV6, V6Directory, status, cancellationToken);
                migrated = true;
            }

            if (Directory.Exists(legacyV5) && !currentV5)
            {
                status?.Invoke("正在迁移 PP-OCRv5 模型...");
                await ModelMigration.CopyDirectoryAsync(
                    legacyV5, V5Directory, status, cancellationToken);
                migrated = true;
            }

            string legacyClassifier = Path.Combine(legacyV6, ClsFileName);
            if (File.Exists(legacyClassifier)
                && (!File.Exists(ClsPath) || new FileInfo(ClsPath).Length < MinClassifierBytes))
            {
                Directory.CreateDirectory(ClsDirectory);
                File.Copy(legacyClassifier, ClsPath, overwrite: true);
                migrated = true;
            }

            if (!File.Exists(ClsPath) || new FileInfo(ClsPath).Length < MinClassifierBytes)
                CopyBundledClassifier(status);

            if (!migrated)
            {
                status?.Invoke("未找到可迁移的旧版 SigXor OCR 模型");
                return false;
            }

            if (GetActiveModelSet() is null || IsUsingLegacy)
            {
                status?.Invoke("OCR 模型迁移后仍不完整");
                return false;
            }

            status?.Invoke("OCR 模型迁移完成");
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            status?.Invoke($"OCR 模型迁移失败：{ex.Message}");
            return false;
        }
    }

    public static bool Delete(out string? error)
    {
        error = null;
        OcrModelSet? set = GetActiveModelSet();
        if (set is null)
        {
            error = "OCR 模型未下载。";
            return false;
        }

        if (set.IsLegacy)
        {
            error = "当前复用的是旧版 SigXor 数据目录中的 OCR 模型，未删除。请在该目录中手动清理。";
            return false;
        }

        try
        {
            foreach (string dir in new[] { V6Directory, V5Directory, ClsDirectory })
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
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
        OcrModelSet? set = GetActiveModelSet();
        string dir = set is null ? ModelsRoot : Path.GetDirectoryName(set.DetPath) ?? ModelsRoot;
        Directory.CreateDirectory(dir);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = dir,
            UseShellExecute = true,
        });
    }

    private static OcrModelSet? TryBuildSet(
        string dir,
        string detName,
        string recName,
        string dictName,
        string clsPath,
        bool isV6,
        long minDet,
        long minRec,
        bool isLegacy)
    {
        string det = Path.Combine(dir, detName);
        string rec = Path.Combine(dir, recName);
        string dict = Path.Combine(dir, dictName);
        if (!File.Exists(det) || new FileInfo(det).Length < minDet) return null;
        if (!File.Exists(rec) || new FileInfo(rec).Length < minRec) return null;
        if (!File.Exists(dict) || new FileInfo(dict).Length < MinDictBytes) return null;
        if (!File.Exists(clsPath) || new FileInfo(clsPath).Length < MinClassifierBytes) return null;
        return new OcrModelSet(det, clsPath, rec, dict, isV6, isLegacy);
    }

    private static string V6DetPath => Path.Combine(V6Directory, V6DetFileName);
    private static string V6RecPath => Path.Combine(V6Directory, V6RecFileName);
    private static string V6DictPath => Path.Combine(V6Directory, V6DictFileName);
    private static string V5DetPath => Path.Combine(V5Directory, V5DetFileName);
    private static string V5RecPath => Path.Combine(V5Directory, V5RecFileName);
    private static string V5DictPath => Path.Combine(V5Directory, V5DictFileName);

    private static void CopyBundledClassifier(Action<string>? status)
    {
        if (File.Exists(ClsPath) && new FileInfo(ClsPath).Length >= MinClassifierBytes) return;

        string baseDir = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(baseDir, "models", "v5", ClsFileName),
            Path.Combine(baseDir, "models", "ocr", "v6", ClsFileName),
        ];

        foreach (string source in candidates)
        {
            if (!File.Exists(source)) continue;
            try
            {
                Directory.CreateDirectory(ClsDirectory);
                File.Copy(source, ClsPath, overwrite: true);
                return;
            }
            catch (Exception ex)
            {
                status?.Invoke($"复制 OCR 分类器失败：{ex.Message}");
            }
        }
    }

    private static async Task<bool> EnsureFileAsync(
        string path,
        string url,
        long minBytes,
        string displayName,
        Action<string>? status,
        CancellationToken cancellationToken)
    {
        if (File.Exists(path) && new FileInfo(path).Length >= minBytes)
            return true;

        string temp = path + ".download";
        try
        {
            if (File.Exists(temp)) File.Delete(temp);
            status?.Invoke($"正在下载{displayName}...");

            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) SigXor/1.2");
            client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
            using var response = await client.GetAsync(
                url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                status?.Invoke($"{displayName}下载失败：HTTP {(int)response.StatusCode}");
                return false;
            }

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
                    status?.Invoke(percent >= 0
                        ? $"正在下载{displayName} {percent}%"
                        : $"正在下载{displayName}（{FormatSize(downloaded)}）");
                }
            }

            if (!File.Exists(temp) || new FileInfo(temp).Length < minBytes)
            {
                status?.Invoke($"{displayName}下载不完整，请检查网络后重试");
                return false;
            }

            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
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
            status?.Invoke($"{displayName}下载失败：{ex.Message}");
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
