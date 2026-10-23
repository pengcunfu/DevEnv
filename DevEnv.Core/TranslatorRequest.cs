namespace DevEnv.Core;

/// <summary>宿主打开/填充熔岩翻译 WebView2 的请求。</summary>
public sealed class TranslatorRequest
{
    public bool Visible { get; init; } = true;
    public string? SiteId { get; init; }
    public string? Url { get; init; }
    /// <summary>为 true 时从系统剪贴板取文本并 DOM 填充。</summary>
    public bool FillClipboard { get; init; }
    /// <summary>显式填充文本；优先于剪贴板。</summary>
    public string? Text { get; init; }
}
