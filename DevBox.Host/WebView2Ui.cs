using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DevBox.Host;

/// <summary>
/// WebView2 壳层 UI：隐藏底部 URL 状态栏；按场景禁用默认菜单或换成中文自定义菜单。
/// </summary>
internal static class WebView2Ui
{
    private static readonly HashSet<string> HiddenNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "saveAs", "saveImage", "saveLinkAs", "print", "share",
        "webCapture", "createQrCode", "emoji", "mediaPictureInPicture",
        "mediaCastToDevice", "other",
    };

    private static readonly Dictionary<string, string> ZhLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["back"] = "后退",
        ["forward"] = "前进",
        ["reload"] = "重新加载",
        ["cut"] = "剪切",
        ["copy"] = "复制",
        ["paste"] = "粘贴",
        ["pasteAndMatchStyle"] = "粘贴为纯文本",
        ["selectAll"] = "全选",
        ["undo"] = "撤销",
        ["redo"] = "重做",
        ["copyLinkToClipboard"] = "复制链接",
        ["copyImageToClipboard"] = "复制图片",
        ["copyImageLocation"] = "复制图片地址",
        ["copyLinkLocation"] = "复制链接地址",
        ["openLinkInNewWindow"] = "在新窗口打开链接",
        ["openLinkInNewTab"] = "在新标签打开链接",
        ["openImageInNewWindow"] = "在新窗口打开图片",
        ["inspectElement"] = "检查",
    };

    /// <param name="customNativeMenu">
    /// true：拦截并显示宿主中文菜单（翻译站外页）；
    /// false：完全禁用默认菜单，交给页面内 React 右键（主壳 UI，对齐 Avalonia 卡片菜单）。
    /// </param>
    /// <param name="allowExternalDrop">
    /// 主壳应为 true：配合 WebViewDropBridge 用页面 drop 取本地路径。
    /// 翻译页可 false，避免外站误吃系统文件。
    /// </param>
    public static void ConfigureShell(
        WebView2 webView,
        bool enableDevTools = true,
        bool customNativeMenu = false,
        bool allowExternalDrop = true)
    {
        var core = webView.CoreWebView2
            ?? throw new InvalidOperationException("CoreWebView2 尚未初始化");

        webView.AllowExternalDrop = allowExternalDrop;

        var settings = core.Settings;
        settings.IsStatusBarEnabled = false;
        settings.IsWebMessageEnabled = true;
        settings.AreDevToolsEnabled = enableDevTools;
        settings.IsZoomControlEnabled = true;
        settings.AreBrowserAcceleratorKeysEnabled = true;

        core.ContextMenuRequested -= OnContextMenuRequested;
        core.NavigationStarting -= OnNavigationStarting;
        core.DownloadStarting -= OnDownloadStarting;

        // 拦截 file:// 导航与本地下载；真正添加走 WebViewDropBridge
        core.NavigationStarting += OnNavigationStarting;
        core.DownloadStarting += OnDownloadStarting;

        if (customNativeMenu)
        {
            // 必须为 true 才会触发 ContextMenuRequested
            settings.AreDefaultContextMenusEnabled = true;
            core.ContextMenuRequested += OnContextMenuRequested;
        }
        else
        {
            settings.AreDefaultContextMenusEnabled = false;
        }
    }

    private static void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        var uri = e.Uri ?? "";
        if (uri.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            || uri.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || uri.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)
            || uri.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
            || uri.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
        }
    }

    private static void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        var uri = e.DownloadOperation.Uri ?? "";
        // 本地文件拖入触发的「下载」一律取消；真正的添加由 WebViewDropBridge 完成
        if (uri.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            || uri.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(e.ResultFilePath ?? "").Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            e.Handled = true;
        }
    }

    private static void OnContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs args)
    {
        if (sender is not CoreWebView2 core)
            return;

        var host = FindHost(core);
        if (host is null)
        {
            args.Handled = true;
            return;
        }

        var deferral = args.GetDeferral();
        args.Handled = true;

        var menu = new ContextMenuStrip();
        var hasItem = PopulateMenu(menu, args.MenuItems, args, core.Settings.AreDevToolsEnabled);

        if (!hasItem)
        {
            AddCommand(menu, "重新加载", () =>
            {
                try { core.Reload(); } catch { /* ignore */ }
            });
            if (core.Settings.AreDevToolsEnabled)
            {
                menu.Items.Add(new ToolStripSeparator());
                AddCommand(menu, "开发者工具", () =>
                {
                    try { core.OpenDevToolsWindow(); } catch { /* ignore */ }
                });
            }
        }

        void OnClosed(object? s, ToolStripDropDownClosedEventArgs e)
        {
            menu.Closed -= OnClosed;
            deferral.Complete();
            menu.Dispose();
        }

        menu.Closed += OnClosed;
        menu.Show(host.PointToScreen(args.Location));
    }

    private static bool PopulateMenu(
        ContextMenuStrip menu,
        IList<CoreWebView2ContextMenuItem> items,
        CoreWebView2ContextMenuRequestedEventArgs args,
        bool enableDevTools)
    {
        var added = false;
        foreach (var item in items)
        {
            if (item.Kind == CoreWebView2ContextMenuItemKind.Separator)
            {
                if (menu.Items.Count > 0 && menu.Items[^1] is not ToolStripSeparator)
                    menu.Items.Add(new ToolStripSeparator());
                continue;
            }

            if (HiddenNames.Contains(item.Name))
                continue;
            if (string.Equals(item.Name, "inspectElement", StringComparison.OrdinalIgnoreCase) && !enableDevTools)
                continue;
            if (item.Kind == CoreWebView2ContextMenuItemKind.Submenu)
                continue;
            if (item.Kind != CoreWebView2ContextMenuItemKind.Command)
                continue;

            var label = ZhLabels.TryGetValue(item.Name, out var zh) ? zh : item.Label;
            if (string.IsNullOrWhiteSpace(label))
                continue;

            var commandId = item.CommandId;
            var mi = new ToolStripMenuItem(label) { Enabled = item.IsEnabled };
            mi.Click += (_, _) => { args.SelectedCommandId = commandId; };
            menu.Items.Add(mi);
            added = true;
        }

        while (menu.Items.Count > 0 && menu.Items[^1] is ToolStripSeparator)
            menu.Items.RemoveAt(menu.Items.Count - 1);

        return added;
    }

    private static void AddCommand(ContextMenuStrip menu, string text, Action action)
    {
        var mi = new ToolStripMenuItem(text);
        mi.Click += (_, _) => action();
        menu.Items.Add(mi);
    }

    private static Control? FindHost(CoreWebView2 core)
    {
        foreach (Form form in Application.OpenForms)
        {
            var found = FindWebView(form, core);
            if (found is not null)
                return found;
        }
        return null;
    }

    private static WebView2? FindWebView(Control parent, CoreWebView2 core)
    {
        if (parent is WebView2 wv && ReferenceEquals(wv.CoreWebView2, core))
            return wv;
        foreach (Control child in parent.Controls)
        {
            var found = FindWebView(child, core);
            if (found is not null)
                return found;
        }
        return null;
    }
}
