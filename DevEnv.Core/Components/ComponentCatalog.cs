namespace DevEnv.Core.Components;

/// <summary>可选组件的静态定义：id、显示名、大致下载体积与安装后必须存在的关键文件。</summary>
public sealed record ComponentDefinition(
    string Id,
    string DisplayName,
    long ApproximateBytes,
    IReadOnlyList<string> KeyFiles);

/// <summary>
/// 可选组件目录。基础包只保留 Magick.NET；Office / OCR / 语音的托管与原生依赖
/// 通过 GitHub Release 的组件 zip 按需安装到 %LOCALAPPDATA%\DevEnv\Components。
/// </summary>
public static class ComponentCatalog
{
    public const string Office = "office";
    public const string Ocr = "ocr";
    public const string Voice = "voice";

    public static readonly ComponentDefinition OfficePreview = new(
        Office,
        "Office 预览",
        2_100_000,
        ["DocumentFormat.OpenXml.dll"]);

    public static readonly ComponentDefinition OcrEngine = new(
        Ocr,
        "OCR 引擎",
        11_700_000,
        ["RapidOcrNet.dll", "libSkiaSharp.dll", "onnxruntime.dll"]);

    public static readonly ComponentDefinition VoiceEngine = new(
        Voice,
        "语音引擎",
        7_600_000,
        ["sherpa-onnx-c-api.dll", "onnxruntime.dll"]);

    public static readonly IReadOnlyList<ComponentDefinition> All =
        [OfficePreview, OcrEngine, VoiceEngine];

    public static ComponentDefinition? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        return All.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}
