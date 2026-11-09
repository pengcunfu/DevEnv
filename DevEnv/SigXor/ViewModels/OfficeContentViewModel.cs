using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Web.WebView2.Core;
using DevEnv.Core.Components;
using DevEnv.Core.SigXor.Office;
using DevEnv.Host.SigXor.Services;
using CoreServices = DevEnv.Core.Services.CoreServices;

namespace DevEnv.Host.SigXor.ViewModels;

/// <summary>
/// Office 文档内容视图模型：双引擎状态机。
/// 结构化引擎在后台线程解析（Core 产出 Word/Sheet/Slides 模型）；
/// PPT 在本机有 PowerPoint 时额外导出整页 PNG 做视觉预览；
/// 网页引擎由视图侧 WebView2 承载，二者经 UseWeb 一键切换。
/// </summary>
public sealed partial class OfficeContentViewModel : ObservableObject, IDisposable
{
    /// <summary>网页引擎可渲染的扩展名（WebView2 视图按此选择 JS 渲染库）。</summary>
    public static readonly HashSet<string> WebRenderableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".docx", ".docm", ".xlsx", ".xlsm", ".xls", ".pptx", ".pptm",
    };

    private static readonly HashSet<string> PresentationExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ppt", ".pptx", ".pptm", ".pps", ".ppsx", ".ppsm",
    };

    /// <summary>
    /// PPTX 网页预览体积上限。PPTXjs 需把整文件载入 JS 堆，过大极易拖垮 WebView2 渲染进程。
    /// </summary>
    public const long MaxWebPptxBytes = 8L * 1024 * 1024;

    private readonly string _path;
    private readonly string _extension;
    private readonly long _fileLength;
    private string? _slideCacheDir;
    private CancellationTokenSource? _loadCts;

    public OfficeContentViewModel(string path)
    {
        _path = path;
        _extension = Path.GetExtension(path).ToLowerInvariant();
        try { _fileLength = new FileInfo(path).Length; }
        catch { _fileLength = 0; }

        IsWebOnly = _extension is ".xls"; // 旧版 Excel 仅 SheetJS 可读
        CanUseWeb = ComputeCanUseWeb(out var blockReason);
        WebBlockReason = blockReason;
        IsStructuredAvailable = CoreServices.Components.IsInstalled(ComponentCatalog.Office);

        // 缺 Office 组件时优先网页引擎；没有 WebView2 才退到安装提示。
        if (!IsStructuredAvailable && CanUseWeb && OfficeDocumentReader.CanReadStructured(_extension))
        {
            UseWeb = true;
            StructuredNotice = "未安装 Office 结构化预览组件，当前使用网页引擎。" +
                               "可在 设置 → 服务与模型 中安装（约 2 MB）。";
        }
        else
        {
            // pptx/pptm 默认可走网页引擎：本机 PowerPoint COM 导 PNG 往往数秒起，体感远慢于 WebView。
            // 结构化画面预览仍可在工具栏一键切换。xls 只能网页。
            UseWeb = IsWebOnly || (CanUseWeb && _extension is ".pptx" or ".pptm");
        }

        LoadCommand = new AsyncRelayCommand(LoadAsync);
    }

    private bool ComputeCanUseWeb(out string? blockReason)
    {
        blockReason = null;
        if (!WebRenderableExtensions.Contains(_extension)) return false;
        if (!IsWebViewRuntimeAvailable())
        {
            blockReason = "未检测到 WebView2 运行时。";
            return false;
        }
        if (_extension is ".pptx" or ".pptm" && _fileLength > MaxWebPptxBytes)
        {
            blockReason =
                $"此 PPT 约 {FormatSize(_fileLength)}，超过网页预览上限 {FormatSize(MaxWebPptxBytes)}。" +
                "请使用结构化预览（本机已装 PowerPoint 时会导出整页画面）。";
            return false;
        }
        return true;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        double kb = bytes / 1024.0;
        if (kb < 1024) return $"{kb:0.#} KB";
        return $"{kb / 1024.0:0.#} MB";
    }

    private static bool IsWebViewRuntimeAvailable()
    {
        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            return !string.IsNullOrEmpty(version);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView2 runtime check failed: {ex.Message}");
            return false;
        }
    }

    public string FilePath => _path;

    public string WebKind => _extension switch
    {
        ".docx" or ".docm" => "docx",
        ".xlsx" or ".xlsm" or ".xls" => "xlsx",
        ".pptx" or ".pptm" => "pptx",
        _ => "",
    };

    public bool IsWebOnly { get; }
    public bool CanUseWeb { get; }
    public string? WebBlockReason { get; }

    /// <summary>Office 结构化预览组件（DocumentFormat.OpenXml）是否已安装。</summary>
    public bool IsStructuredAvailable { get; }

    public IAsyncRelayCommand LoadCommand { get; }

    public enum LoadState { Loading, Loaded, Error, Unsupported }

    [ObservableProperty]
    private LoadState _state = LoadState.Loading;

    [ObservableProperty]
    private object? _structured;

    [ObservableProperty]
    private string? _error;

    [ObservableProperty]
    private string? _structuredNotice;

    [ObservableProperty]
    private bool _useWeb;

    [ObservableProperty]
    private string? _webError;

    [ObservableProperty]
    private string _loadingMessage = "正在解析文档…";

    partial void OnUseWebChanged(bool value)
    {
        if (value)
        {
            WebError = null;
        }
        else if (Structured is null && State != LoadState.Loading)
        {
            // 默认网页预览时跳过了结构化/COM 导图，切回来再按需加载
            _ = LoadAsync();
        }
        RaiseEngineChanged();
    }

    partial void OnStructuredChanged(object? value) => RaiseEngineChanged();

    partial void OnStateChanged(LoadState value) => RaiseEngineChanged();

    private void RaiseEngineChanged()
    {
        OnPropertyChanged(nameof(ShowStructured));
        OnPropertyChanged(nameof(ShowWeb));
        OnPropertyChanged(nameof(ShowWord));
        OnPropertyChanged(nameof(ShowSheet));
        OnPropertyChanged(nameof(ShowSlides));
    }

    public bool ShowStructured => !UseWeb && State is LoadState.Loaded;
    public bool ShowWeb => UseWeb && CanUseWeb;
    public bool ShowWord => ShowStructured && Structured is WordBlocksModel;
    public bool ShowSheet => ShowStructured && Structured is SheetSetModel;
    public bool ShowSlides => ShowStructured && Structured is SlidesModel;

    [RelayCommand]
    private void UseStructuredView()
    {
        if (!IsStructuredAvailable)
        {
            StructuredNotice = "未安装 Office 结构化预览组件。请到 设置 → 服务与模型 安装（约 2 MB）。";
            return;
        }
        UseWeb = false;
    }

    [RelayCommand]
    private void UseWebView()
    {
        if (!CanUseWeb)
        {
            WebError = WebBlockReason ?? "当前文件不支持网页预览。";
            return;
        }
        UseWeb = true;
    }

    public void ReportWebEngineCrash(string detail)
    {
        WebError = detail;
        if (!IsWebOnly) UseWeb = false;
    }

    private async Task LoadAsync()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        State = LoadState.Loading;
        LoadingMessage = "正在解析文档…";
        StructuredNotice = IsStructuredAvailable
            ? null
            : "未安装 Office 结构化预览组件，当前使用网页引擎。可在 设置 → 服务与模型 中安装（约 2 MB）。";
        Error = null;

        if (!IsStructuredAvailable && OfficeDocumentReader.CanReadStructured(_extension) && !CanUseWeb)
        {
            State = LoadState.Unsupported;
            StructuredNotice = "未安装 Office 结构化预览组件。请到 设置 → 服务与模型 安装（约 2 MB）后重试。";
            return;
        }

        bool isPresentation = PresentationExtensions.Contains(_extension);

        // 默认网页预览时跳过 PowerPoint COM 导出（导 PNG 是最大耗时点）。
        // 仅在用户切到结构化视图时才启动本机 PowerPoint。
        if (!UseWeb && isPresentation && PowerPointSlideExport.IsAvailable())
        {
            try
            {
                LoadingMessage = "正在用 PowerPoint 渲染幻灯片画面…";
                AppPaths.EnsureCreated();
                ClearSlideCache();
                _slideCacheDir = Path.Combine(AppPaths.PreviewCacheDirectory, Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_slideCacheDir);

                var pngPaths = await PowerPointSlideExport.ExportAsync(_path, _slideCacheDir, ct);
                ct.ThrowIfCancellationRequested();

                SlidesModel? textModel = null;
                if (OfficeDocumentReader.CanReadStructured(_extension))
                {
                    try
                    {
                        LoadingMessage = "正在提取幻灯片文字…";
                        textModel = await Task.Run(() => (SlidesModel)OfficeDocumentReader.Read(_path), ct);
                    }
                    catch
                    {
                        // 文字提取失败不影响画面预览
                    }
                }

                Structured = MergeRenderedSlides(textModel, pngPaths);
                State = LoadState.Loaded;
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                StructuredNotice = $"PowerPoint 画面导出失败（{ex.Message}），将尝试文字/内嵌图预览。";
                ClearSlideCache();
            }
        }
        else if (!UseWeb && isPresentation && !PowerPointSlideExport.IsAvailable())
        {
            StructuredNotice =
                "未检测到 Microsoft PowerPoint。安装后可导出整页画面预览；当前仅显示文字与内嵌图片。";
        }

        // 网页预览：尽快结束 Loading，让视图挂上 WebView 开始拉文档
        if (UseWeb && CanUseWeb)
        {
            State = LoadState.Loaded;
            return;
        }

        if (!OfficeDocumentReader.CanReadStructured(_extension))
        {
            State = LoadState.Unsupported;
            StructuredNotice ??= _extension is ".xls"
                ? "旧版 .xls 二进制格式由网页引擎（SheetJS）读取。"
                : $"旧版 {_extension} 暂不支持结构化预览。" +
                  (isPresentation ? "请安装 Microsoft PowerPoint 以启用画面预览。" : "可用网页预览或十六进制查看。");
            return;
        }

        try
        {
            LoadingMessage = "正在解析文档…";
            var model = await Task.Run(() => OfficeDocumentReader.Read(_path), ct);
            ct.ThrowIfCancellationRequested();
            Structured = model;
            State = LoadState.Loaded;
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (NotSupportedException)
        {
            State = LoadState.Unsupported;
            StructuredNotice = $"此格式暂不支持结构化解析（{_extension}）。";
        }
        catch (IOException ex)
        {
            State = LoadState.Error;
            Error = $"文件读取失败: {ex.Message}";
            if (CanUseWeb)
            {
                UseWeb = true;
                StructuredNotice = $"文件读取错误（{ex.Message}），已切换到网页预览。";
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            State = LoadState.Error;
            Error = $"访问权限不足: {ex.Message}";
            if (CanUseWeb)
            {
                UseWeb = true;
                StructuredNotice = $"访问权限不足（{ex.Message}），已切换到网页预览。";
            }
        }
        catch (InvalidDataException ex)
        {
            State = LoadState.Error;
            Error = ex.Message;
            if (CanUseWeb)
            {
                UseWeb = true;
                StructuredNotice = $"文档格式问题（{ex.Message}），已切换到网页预览。";
            }
        }
        catch (OutOfMemoryException)
        {
            State = LoadState.Error;
            Error = "文档过大，内存不足";
            StructuredNotice = "文档过大，解析失败。";
        }
        catch (Exception ex)
        {
            State = LoadState.Error;
            Error = $"解析失败: {ex.Message}";
            if (CanUseWeb)
            {
                UseWeb = true;
                StructuredNotice = $"结构化解析失败（{ex.Message}），已切换到网页预览。";
            }
        }
    }

    private static SlidesModel MergeRenderedSlides(SlidesModel? textModel, IReadOnlyList<string> pngPaths)
    {
        var slides = new List<SlideData>(pngPaths.Count);
        for (int i = 0; i < pngPaths.Count; i++)
        {
            var text = textModel?.Slides.ElementAtOrDefault(i);
            slides.Add(new SlideData
            {
                Index = i + 1,
                Title = text?.Title ?? "",
                Lines = text?.Lines ?? Array.Empty<string>(),
                Images = text?.Images ?? Array.Empty<SlideImageData>(),
                RenderedImagePath = pngPaths[i],
            });
        }

        return new SlidesModel
        {
            Slides = slides,
            SlideWidthEmu = textModel?.SlideWidthEmu ?? 0,
            SlideHeightEmu = textModel?.SlideHeightEmu ?? 0,
            ImagesTruncated = textModel?.ImagesTruncated ?? false,
        };
    }

    private void ClearSlideCache()
    {
        if (_slideCacheDir is null) return;
        try
        {
            if (Directory.Exists(_slideCacheDir))
                Directory.Delete(_slideCacheDir, recursive: true);
        }
        catch { /* ignore */ }
        _slideCacheDir = null;
    }

    public void Dispose()
    {
        try { _loadCts?.Cancel(); } catch { }
        _loadCts?.Dispose();
        _loadCts = null;
        ClearSlideCache();
        Structured = null;
    }
}
