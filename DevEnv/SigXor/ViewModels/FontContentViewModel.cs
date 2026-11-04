using System.Globalization;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DevEnv.Core.SigXor;

namespace DevEnv.Host.SigXor.ViewModels;

/// <summary>字体预览：ttf/otf/ttc 用 GlyphTypeface；woff/woff2 给出明确提示。</summary>
public sealed partial class FontContentViewModel : ObservableObject
{
    public const string SampleLatin = "ABCDEFGHIJKLMNOPQRSTUVWXYZ\nabcdefghijklmnopqrstuvwxyz\n0123456789";
    public const string SampleChinese = "天地玄黄 宇宙洪荒\n永和九年 兰亭序\n汉体预览 字体测试";
    public const string SampleSymbols = "!@#$%^&*()_+-=[]{}|;:',.<>?/";

    public FontContentViewModel(string path, string name, long length)
    {
        FilePath = path;
        Name = name;
        Length = length;
        SizeText = FileEntry.FormatSize(length);

        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".woff" or ".woff2")
        {
            Error = "WOFF/WOFF2 需浏览器解包。当前请使用 TTF/OTF/TTC；或在网页里用 @font-face 查看。";
            FamilyName = Path.GetFileNameWithoutExtension(path);
            FaceName = ext.TrimStart('.').ToUpperInvariant();
            Summary = $"{FamilyName} · {FaceName} · {SizeText}";
            return;
        }

        try
        {
            var uri = new Uri(path, UriKind.Absolute);
            var typeface = new GlyphTypeface(uri);
            FamilyName = PickName(typeface.FamilyNames) ?? Path.GetFileNameWithoutExtension(path);
            FaceName = PickName(typeface.FaceNames) ?? "";
            string? ver = PickName(typeface.VersionStrings);
            VersionText = string.IsNullOrWhiteSpace(ver) ? null : ver;
            FontFamily = new FontFamily(uri, "./#" + FamilyName);
            CanPreview = true;
            Summary = $"{FamilyName}" +
                      (string.IsNullOrEmpty(FaceName) ? "" : $" · {FaceName}") +
                      $" · {SizeText}";
        }
        catch (Exception ex)
        {
            Error = $"无法加载字体：{ex.Message}";
            FamilyName = Path.GetFileNameWithoutExtension(path);
            FaceName = "";
            Summary = $"{FamilyName} · {SizeText}";
        }
    }

    public string FilePath { get; }
    public string Name { get; }
    public long Length { get; }
    public string SizeText { get; }
    public string FamilyName { get; } = "";
    public string FaceName { get; } = "";
    public string? VersionText { get; }
    public string Summary { get; } = "";
    public bool CanPreview { get; }
    public FontFamily? FontFamily { get; }

    [ObservableProperty]
    private string? _error;

    private static string? PickName(IDictionary<CultureInfo, string> names)
    {
        if (names.Count == 0) return null;
        foreach (var kv in names)
        {
            string tag = kv.Key.IetfLanguageTag;
            if (tag.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(kv.Value))
                return kv.Value;
        }
        foreach (var kv in names)
        {
            string tag = kv.Key.IetfLanguageTag;
            if (tag.StartsWith("en", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(kv.Value))
                return kv.Value;
        }
        return names.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }
}
