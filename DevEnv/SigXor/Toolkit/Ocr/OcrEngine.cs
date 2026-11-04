using System.IO;
using System.Threading;
using System.Windows.Media.Imaging;
using RapidOcrNet;

namespace DevEnv.Host.SigXor.Toolkit.Ocr;

public sealed class OcrEngine : IDisposable
{
    private readonly object _lock = new();
    private RapidOcr? _ocr;
    private OcrModelSet? _modelSet;

    public string? LastError { get; private set; }

    public async Task<string?> RecognizeAsync(
        BitmapSource image,
        Action<string>? status = null,
        CancellationToken cancellationToken = default)
    {
        LastError = null;
        if (!await OcrModelManager.EnsureReadyAsync(status, cancellationToken))
        {
            LastError = "OCR 模型未就绪或下载失败";
            return null;
        }

        OcrModelSet? set = OcrModelManager.GetActiveModelSet();
        if (set is null)
        {
            LastError = "未找到可用的 OCR 模型";
            return null;
        }

        string tempFile = Path.Combine(
            Path.GetTempPath(), $"see_ocr_{Guid.NewGuid():N}.png");
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using (FileStream stream = File.Create(tempFile))
                encoder.Save(stream);

            return await Task.Run(
                () => RecognizeCore(tempFile, set, status, cancellationToken),
                cancellationToken);
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
        }
    }

    private string? RecognizeCore(
        string imagePath,
        OcrModelSet set,
        Action<string>? status,
        CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_ocr is null || !Equals(_modelSet, set))
            {
                status?.Invoke("正在加载 OCR 模型...");
                _ocr?.Dispose();
                _ocr = CreateEngine(set);
                _modelSet = set;
            }

            status?.Invoke("正在识别文字...");
            cancellationToken.ThrowIfCancellationRequested();

            RapidOcrOptions options = set.IsV6 ? RapidOcrOptions.PPOCRv6 : RapidOcrOptions.Default;
            var result = _ocr.Detect(imagePath, options);
            string? text = result?.StrRes?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
    }

    private static RapidOcr CreateEngine(OcrModelSet set)
    {
        var ocr = new RapidOcr();
        int threads = Math.Max(1, Environment.ProcessorCount / 2);
        if (set.IsV6)
        {
            var v6 = RapidOcrModelSet.PPOCRv6Small with
            {
                DetModelPath = set.DetPath,
                ClsModelPath = set.ClsPath,
                RecModelPath = set.RecPath,
                KeysPath = set.DictPath,
            };
            ocr.InitModels(v6, threads);
        }
        else
        {
            ocr.InitModels(set.DetPath, set.ClsPath, set.RecPath, set.DictPath, threads);
        }
        return ocr;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _ocr?.Dispose();
            _ocr = null;
            _modelSet = null;
        }
    }
}
