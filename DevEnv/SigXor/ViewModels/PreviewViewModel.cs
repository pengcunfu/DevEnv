using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using DevEnv.Core.SigXor;
using DevEnv.Host.SigXor.Services;

namespace DevEnv.Host.SigXor.ViewModels;

/// <summary>预览层状态：加载并持有当前文件的内容视图模型。</summary>
public partial class PreviewViewModel : ObservableObject
{
    public const long MaxTextPreviewBytes = 100L * 1024 * 1024;

    private readonly BackupService _backup;
    private readonly SettingsService _settings;
    private CancellationTokenSource? _folderCts;

    public PreviewViewModel(SettingsService settings, BackupService backup)
    {
        _settings = settings;
        _backup = backup;
    }

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private FileEntry? _currentFile;

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _subtitle = "";

    [ObservableProperty]
    private object? _content;

    public bool HasUnsavedChanges => Content switch
    {
        TextContentViewModel t => t.IsDirty,
        HexContentViewModel h => h.IsDirty,
        MarkdownContentViewModel m => m.Source.IsDirty,
        _ => false,
    };

    public async Task LoadAsync(FileEntry file)
    {
        if (CurrentFile?.FullPath == file.FullPath && Content is not null) return;
        CloseDocument();
        CurrentFile = file;
        Title = file.Name;
        Subtitle = $"{file.FullPath} · {file.SizeText} · {file.KindText}";

        if (file.IsDirectory)
        {
            await LoadFolderAsync(file);
            return;
        }

        ContentKind kind = FileTypeDetector.Detect(file.FullPath);
        switch (kind)
        {
            case ContentKind.Text:
            case ContentKind.Code:
                await LoadTextAsync(file);
                break;
            case ContentKind.Image:
                LoadImage(file);
                break;
            case ContentKind.Svg:
                LoadSvg(file);
                break;
            case ContentKind.Archive:
                LoadArchive(file);
                break;
            case ContentKind.Csv:
                await LoadCsvAsync(file);
                break;
            case ContentKind.Font:
                LoadFont(file);
                break;
            case ContentKind.Document:
                LoadOffice(file);
                break;
            case ContentKind.Markdown:
                await LoadMarkdownAsync(file);
                break;
            case ContentKind.WebPage:
                await LoadWebAsync(file);
                break;
            case ContentKind.Audio:
                LoadAudio(file);
                break;
            case ContentKind.Video:
                LoadVideo(file);
                break;
            case ContentKind.Pdf:
                LoadPdf(file);
                break;
            case ContentKind.Binary:
                LoadHex(file);
                break;
            default:
                Content = new InfoContentViewModel(
                    "无法识别此文件类型",
                    "没有找到对应的预览方式，可以尝试用十六进制编辑器查看。",
                    "以十六进制打开",
                    () => LoadHex(file));
                break;
        }
    }

    public void LoadHex(FileEntry file)
    {
        try
        {
            CloseDocument();
            var doc = HexDocument.Open(file.FullPath);
            Content = new HexContentViewModel(doc, file.FullPath, _backup,
                _settings.Current.HexFontSize, _settings.Current.BytesPerRow);
        }
        catch (Exception ex)
        {
            Content = new InfoContentViewModel("打开失败", ex.Message);
        }
    }

    public async Task<bool> SaveIfDirtyAsync()
    {
        if (!HasUnsavedChanges) return true;
        var result = MessageBox.Show(
            $"“{CurrentFile?.Name}” 有未保存的修改，是否保存？",
            "SigXor",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result == MessageBoxResult.Cancel) return false;
        if (result == MessageBoxResult.No) return true;

        try
        {
            switch (Content)
            {
                case TextContentViewModel t:
                    var save = t.SaveCommand;
                    save.Execute(null);
                    if (save.ExecutionTask is not null) await save.ExecutionTask;
                    break;
                case MarkdownContentViewModel m:
                    var mdSave = m.Source.SaveCommand;
                    mdSave.Execute(null);
                    if (mdSave.ExecutionTask is not null) await mdSave.ExecutionTask;
                    break;
                case HexContentViewModel h:
                    h.SaveCommand.Execute(null);
                    break;
            }
            return !HasUnsavedChanges;
        }
        catch
        {
            return false;
        }
    }

    public void CloseDocument()
    {
        try { _folderCts?.Cancel(); } catch { /* ignore */ }
        _folderCts?.Dispose();
        _folderCts = null;

        if (Content is HexContentViewModel hex) hex.Dispose();
        if (Content is OfficeContentViewModel office) office.Dispose();
        if (Content is MarkdownContentViewModel markdown) markdown.Dispose();
        if (Content is WebContentViewModel web) web.Dispose();
        Content = null;
    }

    private async Task LoadFolderAsync(FileEntry file)
    {
        _folderCts?.Cancel();
        _folderCts?.Dispose();
        _folderCts = new CancellationTokenSource();
        CancellationToken ct = _folderCts.Token;

        DirectoryInfo di;
        try
        {
            di = new DirectoryInfo(file.FullPath);
            if (!di.Exists) throw new DirectoryNotFoundException(file.FullPath);
        }
        catch (Exception ex)
        {
            Content = new InfoContentViewModel("无法打开文件夹", ex.Message);
            Subtitle = $"{file.FullPath} · 文件夹";
            return;
        }

        string created = di.CreationTime.ToString("yyyy-MM-dd HH:mm:ss");
        string modified = di.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
        string attrs = FormatFileAttributes(di.Attributes);
        var info = new InfoContentViewModel(
            "文件夹",
            BuildFolderMessage(file.FullPath, created, modified, attrs, "正在计算…"));
        Content = info;
        Subtitle = $"{file.FullPath} · 文件夹";

        try
        {
            DirectoryStats stats = await Task.Run(() => FileSystemService.InspectDirectory(file.FullPath, ct), ct);
            if (ct.IsCancellationRequested || !ReferenceEquals(Content, info)) return;

            string sizeText = FileEntry.FormatSize(stats.TotalBytes);
            string counts = $"{stats.FileCount:N0} 个文件 · {stats.DirectoryCount:N0} 个子文件夹";
            if (stats.Skipped > 0) counts += $" · {stats.Skipped:N0} 项无法访问";
            info.Message = BuildFolderMessage(file.FullPath, created, modified, attrs, sizeText, counts);
            Subtitle = $"{file.FullPath} · {sizeText} · 文件夹";
        }
        catch (OperationCanceledException)
        {
            // 切换预览时取消
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(Content, info))
                info.Message = BuildFolderMessage(file.FullPath, created, modified, attrs, $"计算失败（{ex.Message}）");
        }
    }

    private static string BuildFolderMessage(
        string path,
        string created,
        string modified,
        string attributes,
        string sizeText,
        string? counts = null)
    {
        var lines = new List<string>
        {
            path,
            "",
            $"大小：{sizeText}",
            $"创建时间：{created}",
            $"修改时间：{modified}",
            $"属性：{attributes}",
        };
        if (!string.IsNullOrEmpty(counts))
            lines.Add($"包含：{counts}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatFileAttributes(FileAttributes attributes)
    {
        var parts = new List<string>();
        if (attributes.HasFlag(FileAttributes.ReadOnly)) parts.Add("只读");
        if (attributes.HasFlag(FileAttributes.Hidden)) parts.Add("隐藏");
        if (attributes.HasFlag(FileAttributes.System)) parts.Add("系统");
        if (attributes.HasFlag(FileAttributes.Compressed)) parts.Add("压缩");
        if (attributes.HasFlag(FileAttributes.Encrypted)) parts.Add("加密");
        if (attributes.HasFlag(FileAttributes.ReparsePoint)) parts.Add("链接/重解析点");
        return parts.Count == 0 ? "普通" : string.Join(" · ", parts);
    }

    private async Task LoadTextAsync(FileEntry file)
    {
        var loaded = await TryReadTextAsync(file, "文本加载失败");
        if (loaded is not null)
        {
            var vm = new TextContentViewModel(file.FullPath, loaded.Value.text, loaded.Value.encoding, _backup);
            vm.FontFamily = _settings.Current.TextFontFamily;
            vm.FontSize = _settings.Current.TextFontSize;
            Content = vm;
        }
    }

    /// <summary>文本类加载共用：大小守卫 + 字节读取 + 编码检测；失败时置 Info 内容并返回 null。</summary>
    private async Task<(string text, Encoding encoding)?> TryReadTextAsync(FileEntry file, string errorTitle)
    {
        try
        {
            long length = new FileInfo(file.FullPath).Length;
            if (length > MaxTextPreviewBytes)
            {
                Content = new InfoContentViewModel(
                    "文件过大，文本预览已跳过",
                    $"文件大小 {FileEntry.FormatSize(length)}，超过文本预览上限 {FileEntry.FormatSize(MaxTextPreviewBytes)}。请使用十六进制编辑器查看或编辑。",
                    "以十六进制打开",
                    () => LoadHex(file));
                return null;
            }

            var bytes = await Task.Run(() => ReadFileShared(file.FullPath));
            var encoding = EncodingService.Detect(bytes);
            return (encoding.GetString(bytes), encoding);
        }
        catch (Exception ex)
        {
            Content = new InfoContentViewModel(errorTitle, ex.Message);
            return null;
        }
    }

    /// <summary>以共享只读方式读取文件全部字节，允许被其他进程占用的文件也能打开。</summary>
    private static byte[] ReadFileShared(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var bytes = new byte[fs.Length];
        int offset = 0;
        while (offset < bytes.Length)
        {
            int read = fs.Read(bytes, offset, bytes.Length - offset);
            if (read == 0) break;
            offset += read;
        }
        return bytes;
    }

    private void LoadImage(FileEntry file)
    {
        try
        {
            string ext = Path.GetExtension(file.FullPath);
            bool isHeif = ext.Equals(".heic", StringComparison.OrdinalIgnoreCase)
                          || ext.Equals(".heif", StringComparison.OrdinalIgnoreCase);

            BitmapSource image = isHeif
                ? DecodeViaWic(file.FullPath)
                : DecodeBitmapImage(ReadFileShared(file.FullPath));

            Content = new ImageContentViewModel(image, file.FullPath, file.Length);
        }
        catch (Exception ex)
        {
            string ext = Path.GetExtension(file.FullPath);
            bool isHeif = ext.Equals(".heic", StringComparison.OrdinalIgnoreCase)
                          || ext.Equals(".heif", StringComparison.OrdinalIgnoreCase);
            string message = isHeif
                ? $"{ex.Message}\n\n若仍无法打开，请到 Microsoft Store 安装「HEIF 图像扩展」与「HEVC 视频扩展」后重试。"
                : ex.Message;
            Content = new InfoContentViewModel("图片加载失败", message);
        }
    }

    private static BitmapImage DecodeBitmapImage(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = 2048;
        image.StreamSource = ms;
        image.EndInit();
        image.Freeze();
        return image;
    }

    /// <summary>经 WIC 解码（含 HEIC/HEIF，依赖系统已装编解码器）。</summary>
    private static BitmapSource DecodeViaWic(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count == 0)
            throw new InvalidOperationException("无法解码图像帧。");

        BitmapFrame frame = decoder.Frames[0];
        int w = frame.PixelWidth;
        if (w > 2048)
        {
            double scale = 2048.0 / w;
            var scaled = new TransformedBitmap(frame, new System.Windows.Media.ScaleTransform(scale, scale));
            scaled.Freeze();
            return scaled;
        }

        frame.Freeze();
        return frame;
    }

    private void LoadSvg(FileEntry file)
    {
        if (!Views.WebViewHostBase.IsRuntimeAvailable())
        {
            Content = new InfoContentViewModel(
                "SVG 预览需要 WebView2 运行时",
                "未检测到 WebView2 运行时，无法渲染矢量图。可安装 Evergreen 运行时后重试，或以十六进制查看。",
                "以十六进制打开",
                () => LoadHex(file));
            return;
        }
        Content = new SvgContentViewModel(file.FullPath, file.Name, file.Length);
    }

    private void LoadArchive(FileEntry file)
    {
        try
        {
            Content = new ArchiveContentViewModel(file.FullPath, file.Name, file.Length);
        }
        catch (Exception ex)
        {
            Content = new InfoContentViewModel("压缩包打开失败", ex.Message);
        }
    }

    private async Task LoadCsvAsync(FileEntry file)
    {
        var loaded = await TryReadTextAsync(file, "表格加载失败");
        if (loaded is null) return;
        try
        {
            Content = new CsvContentViewModel(file.FullPath, file.Name, loaded.Value.text);
        }
        catch (Exception ex)
        {
            Content = new InfoContentViewModel("表格解析失败", ex.Message);
        }
    }

    private void LoadFont(FileEntry file)
    {
        try
        {
            Content = new FontContentViewModel(file.FullPath, file.Name, file.Length);
        }
        catch (Exception ex)
        {
            Content = new InfoContentViewModel("字体加载失败", ex.Message);
        }
    }

    /// <summary>Office 文档：结构化解析由视图模型后台执行，网页引擎按需启动。</summary>
    private void LoadOffice(FileEntry file)
    {
        Content = new OfficeContentViewModel(file.FullPath);
    }

    /// <summary>Markdown：源码复用文本编辑栈，渲染经 Markdig 后由 WebView2 承载。</summary>
    private async Task LoadMarkdownAsync(FileEntry file)
    {
        var loaded = await TryReadTextAsync(file, "Markdown 加载失败");
        if (loaded is null) return;

        var source = new TextContentViewModel(file.FullPath, loaded.Value.text, loaded.Value.encoding, _backup);
        source.FontFamily = _settings.Current.TextFontFamily;
        source.FontSize = _settings.Current.TextFontSize;
        var vm = new MarkdownContentViewModel(file.FullPath, source, Views.WebViewHostBase.IsRuntimeAvailable());
        // 先挂上视图，Markdig 与 WebView 附着并行，缩短可感知等待
        Content = vm;
        await vm.RenderAsync();
    }

    /// <summary>本地网页：WebView2 目录映射渲染（脚本启用），可切只读源码。</summary>
    private async Task LoadWebAsync(FileEntry file)
    {
        var vm = new WebContentViewModel(file.FullPath, Views.WebViewHostBase.IsRuntimeAvailable(), _backup, _settings);
        Content = vm;
        await Task.CompletedTask;
    }

    /// <summary>音频：WebView2 播放页；运行时缺失降级为提示卡片。</summary>
    private void LoadAudio(FileEntry file)
    {
        if (!Views.WebViewHostBase.IsRuntimeAvailable())
        {
            Content = new InfoContentViewModel(
                "音频预览需要 WebView2 运行时",
                "未检测到 WebView2 运行时，无法播放音频。可安装 Evergreen 运行时后重试，或以十六进制查看文件头。",
                "以十六进制打开",
                () => LoadHex(file));
            return;
        }
        Content = new AudioContentViewModel(file.FullPath, file.Name, file.Length);
    }

    /// <summary>视频：WebView2 播放页；运行时缺失降级为提示卡片。</summary>
    private void LoadVideo(FileEntry file)
    {
        if (!Views.WebViewHostBase.IsRuntimeAvailable())
        {
            Content = new InfoContentViewModel(
                "视频预览需要 WebView2 运行时",
                "未检测到 WebView2 运行时，无法播放视频。可安装 Evergreen 运行时后重试，或以十六进制查看文件头。",
                "以十六进制打开",
                () => LoadHex(file));
            return;
        }
        Content = new VideoContentViewModel(file.FullPath, file.Name, file.Length);
    }

    /// <summary>PDF：WebView2 内置 PDF 查看器；运行时缺失降级为提示卡片。</summary>
    private void LoadPdf(FileEntry file)
    {
        if (!Views.WebViewHostBase.IsRuntimeAvailable())
        {
            Content = new InfoContentViewModel(
                "PDF 预览需要 WebView2 运行时",
                "未检测到 WebView2 运行时，无法预览 PDF。可安装 Evergreen 运行时后重试，或以十六进制查看文件头。",
                "以十六进制打开",
                () => LoadHex(file));
            return;
        }
        Content = new PdfContentViewModel(file.FullPath);
    }
}
