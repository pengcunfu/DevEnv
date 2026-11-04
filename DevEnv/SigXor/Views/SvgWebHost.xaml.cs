using System.IO;
using Microsoft.Web.WebView2.Core;
using DevEnv.Host.SigXor.ViewModels;

namespace DevEnv.Host.SigXor.Views;

/// <summary>SVG 渲染：目录映射 + 导航到文件本身（Chromium 原生矢量预览）。</summary>
public partial class SvgWebHost : WebViewHostBase
{
    private const string PreviewHost = "svgpreview.local";
    private string? _path;

    public SvgWebHost()
    {
        InitializeComponent();
    }

    public Task LoadAsync(string path)
    {
        _path = path;
        Remap(Core);
        NavigateOrPending(BuildUrl());
        return Task.CompletedTask;
    }

    public override void PrepareForReuse()
    {
        base.PrepareForReuse();
        _path = null;
    }

    protected override void Configure(CoreWebView2 core)
    {
        Remap(core);
        if (_path is not null)
            NavigateOrPending(BuildUrl());
    }

    private void Remap(CoreWebView2? core)
    {
        if (core is null || _path is null) return;
        string dir = Path.GetDirectoryName(_path)!;
        core.SetVirtualHostNameToFolderMapping(
            PreviewHost, dir, CoreWebView2HostResourceAccessKind.Allow);
    }

    private string BuildUrl()
    {
        if (_path is null) return "about:blank";
        return WithCacheBust($"https://{PreviewHost}/{Uri.EscapeDataString(Path.GetFileName(_path))}");
    }
}
