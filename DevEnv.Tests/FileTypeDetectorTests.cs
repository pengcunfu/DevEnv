using DevEnv.Core.SigXor;

namespace DevEnv.Tests.SigXor;

public sealed class FileTypeDetectorTests : IDisposable
{
    private readonly string _dir;

    public FileTypeDetectorTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "SeeNetType_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 忽略 */ }
    }

    [Fact]
    public void Detect_By_Extension()
    {
        Assert.Equal(ContentKind.Text, FileTypeDetector.ByExtension(".txt"));
        Assert.Equal(ContentKind.Code, FileTypeDetector.ByExtension(".cs"));
        Assert.Equal(ContentKind.Code, FileTypeDetector.ByExtension(".JSON"));
        Assert.Equal(ContentKind.Image, FileTypeDetector.ByExtension(".png"));
        Assert.Equal(ContentKind.Binary, FileTypeDetector.ByExtension(".exe"));
        Assert.Equal(ContentKind.Unknown, FileTypeDetector.ByExtension(".xyz123"));
    }

    [Fact]
    public void Detect_New_Kinds_By_Extension()
    {
        Assert.Equal(ContentKind.Markdown, FileTypeDetector.ByExtension(".md"));
        Assert.Equal(ContentKind.Markdown, FileTypeDetector.ByExtension(".MD"));
        Assert.Equal(ContentKind.Markdown, FileTypeDetector.ByExtension(".markdown"));
        Assert.Equal(ContentKind.Markdown, FileTypeDetector.ByExtension(".mkd"));

        Assert.Equal(ContentKind.WebPage, FileTypeDetector.ByExtension(".html"));
        Assert.Equal(ContentKind.WebPage, FileTypeDetector.ByExtension(".HTML"));
        Assert.Equal(ContentKind.WebPage, FileTypeDetector.ByExtension(".htm"));
        Assert.Equal(ContentKind.WebPage, FileTypeDetector.ByExtension(".xhtml"));

        Assert.Equal(ContentKind.Audio, FileTypeDetector.ByExtension(".mp3"));
        Assert.Equal(ContentKind.Audio, FileTypeDetector.ByExtension(".wav"));
        Assert.Equal(ContentKind.Audio, FileTypeDetector.ByExtension(".flac"));
        Assert.Equal(ContentKind.Audio, FileTypeDetector.ByExtension(".ogg"));
        Assert.Equal(ContentKind.Audio, FileTypeDetector.ByExtension(".m4a"));
        Assert.Equal(ContentKind.Audio, FileTypeDetector.ByExtension(".wma"));

        Assert.Equal(ContentKind.Video, FileTypeDetector.ByExtension(".mp4"));
        Assert.Equal(ContentKind.Video, FileTypeDetector.ByExtension(".webm"));
        Assert.Equal(ContentKind.Video, FileTypeDetector.ByExtension(".mov"));

        Assert.Equal(ContentKind.Pdf, FileTypeDetector.ByExtension(".pdf"));
        Assert.Equal(ContentKind.Pdf, FileTypeDetector.ByExtension(".PDF"));

        Assert.Equal(ContentKind.Svg, FileTypeDetector.ByExtension(".svg"));
        Assert.Equal(ContentKind.Svg, FileTypeDetector.ByExtension(".SVG"));
        Assert.Equal(ContentKind.Image, FileTypeDetector.ByExtension(".heic"));
        Assert.Equal(ContentKind.Image, FileTypeDetector.ByExtension(".heif"));
        Assert.Equal(ContentKind.Archive, FileTypeDetector.ByExtension(".zip"));
        Assert.Equal(ContentKind.Archive, FileTypeDetector.ByExtension(".7z"));
        Assert.Equal(ContentKind.Archive, FileTypeDetector.ByExtension(".rar"));
        Assert.Equal(ContentKind.Csv, FileTypeDetector.ByExtension(".csv"));
        Assert.Equal(ContentKind.Csv, FileTypeDetector.ByExtension(".tsv"));
        Assert.Equal(ContentKind.Font, FileTypeDetector.ByExtension(".ttf"));
        Assert.Equal(ContentKind.Font, FileTypeDetector.ByExtension(".otf"));
        Assert.Equal(ContentKind.Font, FileTypeDetector.ByExtension(".woff"));
        Assert.Equal(ContentKind.Font, FileTypeDetector.ByExtension(".woff2"));
    }

    [Fact]
    public void Detect_Old_Kinds_Unaffected_By_Split()
    {
        // 从旧集合移出的扩展名之外，原有分类保持不变
        Assert.Equal(ContentKind.Code, FileTypeDetector.ByExtension(".xml"));
        Assert.Equal(ContentKind.Text, FileTypeDetector.ByExtension(".log"));
        Assert.Equal(ContentKind.Video, FileTypeDetector.ByExtension(".mp4"));
        Assert.Equal(ContentKind.Video, FileTypeDetector.ByExtension(".webm"));
        Assert.Equal(ContentKind.Video, FileTypeDetector.ByExtension(".mkv"));
        Assert.Equal(ContentKind.Image, FileTypeDetector.ByExtension(".webp"));
    }

    [Fact]
    public void Detect_By_Magic_Bytes()
    {
        Assert.Equal(ContentKind.Image, FileTypeDetector.ByMagic([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]));
        Assert.Equal(ContentKind.Image, FileTypeDetector.ByMagic([0xFF, 0xD8, 0xFF, 0xE0]));
        Assert.Equal(ContentKind.Binary, FileTypeDetector.ByMagic([0x4D, 0x5A, 0x90, 0x00]));
        Assert.Equal(ContentKind.Archive, FileTypeDetector.ByMagic([0x50, 0x4B, 0x03, 0x04]));
        Assert.Equal(ContentKind.Archive, FileTypeDetector.ByMagic([0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C]));
        Assert.Equal(ContentKind.Pdf, FileTypeDetector.ByMagic([0x25, 0x50, 0x44, 0x46]));
        Assert.Equal(ContentKind.Video, FileTypeDetector.ByMagic([0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D]));
        Assert.Equal(ContentKind.Image, FileTypeDetector.ByMagic([0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x68, 0x65, 0x69, 0x63]));
        Assert.Equal(ContentKind.Font, FileTypeDetector.ByMagic([0x00, 0x01, 0x00, 0x00]));
        Assert.Equal(ContentKind.Font, FileTypeDetector.ByMagic([(byte)'O', (byte)'T', (byte)'T', (byte)'O']));
        Assert.Equal(ContentKind.Svg, FileTypeDetector.ByMagic(System.Text.Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>")));
        Assert.Equal(ContentKind.Unknown, FileTypeDetector.ByMagic([0x41, 0x42, 0x43, 0x44]));
    }

    [Fact]
    public void Detect_Unknown_Content_Text_Vs_Binary()
    {
        string textPath = Path.Combine(_dir, "sample.unknownext");
        File.WriteAllText(textPath, "hello world, 中文内容");
        Assert.Equal(ContentKind.Text, FileTypeDetector.Detect(textPath));

        string binPath = Path.Combine(_dir, "sample2.unknownext");
        File.WriteAllBytes(binPath, [0x00, 0x01, 0x02, 0xFF, 0x00]);
        Assert.Equal(ContentKind.Binary, FileTypeDetector.Detect(binPath));
    }

    [Fact]
    public void LooksLikeText_Rejects_Nul()
    {
        Assert.False(FileTypeDetector.LooksLikeText([0x41, 0x00, 0x42]));
        Assert.True(FileTypeDetector.LooksLikeText(System.Text.Encoding.UTF8.GetBytes("中文abc")));
    }
}
