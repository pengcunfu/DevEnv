using CommunityToolkit.Mvvm.ComponentModel;
using DevEnv.Core.SigXor;

namespace DevEnv.Host.SigXor.ViewModels;

/// <summary>视频预览内容：元数据展示 + 播放错误上报（播放器由 WebView2 页承载）。</summary>
public sealed partial class VideoContentViewModel : ObservableObject, IDisposable
{
    public VideoContentViewModel(string path, string name, long length)
    {
        FilePath = path;
        Name = name;
        Length = length;
        SizeText = FileEntry.FormatSize(length);
    }

    public string FilePath { get; }
    public string Name { get; }
    public long Length { get; }
    public string SizeText { get; }

    /// <summary>播放失败信息（编解码不支持 / 读取失败，由播放页 postMessage 回传）。</summary>
    [ObservableProperty]
    private string? _error;

    public void Dispose()
    {
    }
}
