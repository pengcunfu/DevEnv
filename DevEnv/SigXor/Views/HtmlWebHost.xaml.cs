using System.IO;
using Microsoft.Web.WebView2.Core;
using DevEnv.Host.SigXor.ViewModels;

namespace DevEnv.Host.SigXor.Views;

/// <summary>
/// 本地网页渲染宿主：文件所在目录映射为虚拟主机 preview.local，
/// 直接导航到映射 URL —— 相对引用（./img、style.css）按原目录天然解析。
/// 脚本启用（用户决策）；顶级导航与新窗口一律外部打开。
/// </summary>
public partial class HtmlWebHost : WebViewHostBase
{
    private const string PreviewHost = "preview.local";

    private string? _path;

    public HtmlWebHost()
    {
        InitializeComponent();
    }

    /// <summary>文件名含 URL 保留字符（# ?）时映射 URL 无法寻址，上层应切源码模式。</summary>
    public static bool IsPathRenderable(string path)
    {
        string name = Path.GetFileName(path);
        return !name.Contains('#') && !name.Contains('?');
    }

    /// <summary>加载本地 HTML；常驻复用时每次换文件调用。</summary>
    public Task LoadAsync(string path)
    {
        _path = path;
        if (!IsPathRenderable(path))
        {
            if (DataContext is WebContentViewModel vm)
                vm.RenderError = "文件名含 URL 保留字符（# 或 ?），网页渲染不可用。";
            return Task.CompletedTask;
        }

        RemapPreviewDirectory(Core);
        NavigateOrPending(WithCacheBust(
            $"https://{PreviewHost}/{Uri.EscapeDataString(Path.GetFileName(path))}"));
        return Task.CompletedTask;
    }

    public override void PrepareForReuse()
    {
        base.PrepareForReuse();
        _path = null;
    }

    protected override void Configure(CoreWebView2 core)
    {
        core.NavigationStarting += OnNavigationStarting;
        core.NewWindowRequested += OnNewWindowRequested;

        if (_path is null) return;

        if (!IsPathRenderable(_path))
        {
            if (DataContext is WebContentViewModel vm)
                Dispatcher.Invoke(() => vm.RenderError = "文件名含 URL 保留字符（# 或 ?），网页渲染不可用。");
            return;
        }

        RemapPreviewDirectory(core);
        NavigateOrPending(WithCacheBust(
            $"https://{PreviewHost}/{Uri.EscapeDataString(Path.GetFileName(_path))}"));
    }

    private void RemapPreviewDirectory(CoreWebView2? core)
    {
        if (core is null || _path is null) return;
        string dir = Path.GetDirectoryName(_path)!;
        core.SetVirtualHostNameToFolderMapping(
            PreviewHost, dir, CoreWebView2HostResourceAccessKind.Allow);
    }

    /// <summary>离开 preview.local 的顶级导航取消并交给系统浏览器（外链不内嵌跳转）。</summary>
    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (new Uri(e.Uri).Host == PreviewHost) return;
        e.Cancel = true;
        OpenExternally(e.Uri);
    }

    /// <summary>新窗口一律外部打开，不在预览内弹出。</summary>
    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (e.Uri is not null) OpenExternally(e.Uri);
    }

    private static void OpenExternally(string url)
    {
        try
        {
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // 打开失败忽略（无默认浏览器等场景）
        }
    }

    protected override void OnDetach(CoreWebView2 core)
    {
        core.NavigationStarting -= OnNavigationStarting;
        core.NewWindowRequested -= OnNewWindowRequested;
    }
}
