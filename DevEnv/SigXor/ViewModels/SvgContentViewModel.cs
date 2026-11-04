using CommunityToolkit.Mvvm.ComponentModel;

namespace DevEnv.Host.SigXor.ViewModels;

/// <summary>SVG 预览：WebView2 渲染矢量图。</summary>
public sealed partial class SvgContentViewModel : ObservableObject
{
    public SvgContentViewModel(string path, string name, long length)
    {
        FilePath = path;
        Name = name;
        Length = length;
    }

    public string FilePath { get; }
    public string Name { get; }
    public long Length { get; }

    [ObservableProperty]
    private string? _error;
}
