using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Web.WebView2.Core;

namespace DevBox.Host.WebView;

/// <summary>
/// WebView2 壳层设置（WPF 版）：隐藏状态栏、拦掉本地文件导航与下载、按需把默认右键菜单换成中文。
/// 主界面已经不用 WebView2，这里只服务熔岩翻译窗。
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

    public static void ConfigureShell(
        CoreWebView2 core,
        bool enableDevTools,
        bool allowExternalDrop,
        bool customNativeMenu,
        UIElement? contextMenuTarget = null)
    {
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsWebMessageEnabled = true;
        core.Settings.AreDevToolsEnabled = enableDevTools;
        core.Settings.IsZoomControlEnabled = true;
        core.Settings.AreBrowserAcceleratorKeysEnabled = true;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsPinchZoomEnabled = false;
        core.Settings.IsSwipeNavigationEnabled = false;
        core.Settings.AreHostObjectsAllowed = false;

        core.NavigationStarting -= OnNavigationStarting;
        core.NavigationStarting += OnNavigationStarting;

        if (customNativeMenu)
        {
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.ContextMenuRequested += (_, args) => OnContextMenuRequested(_, args, contextMenuTarget);
        }
        else
        {
            core.Settings.AreDefaultContextMenusEnabled = false;
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

    /// <summary>把 WebView2 的英文右键菜单换成中文；定位用鼠标点，右键菜单本来就该出现在那里。</summary>
    private static void OnContextMenuRequested(
        object? sender,
        CoreWebView2ContextMenuRequestedEventArgs args,
        UIElement? contextMenuTarget)
    {
        if (sender is not CoreWebView2 core)
            return;

        args.Handled = true;
        var menu = new ContextMenu();
        Populate(menu, args.MenuItems, args, core.Settings.AreDevToolsEnabled);

        if (menu.Items.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "重新加载" });
            ((MenuItem)menu.Items[^1]).Click += (_, _) => core.Reload();
            if (core.Settings.AreDevToolsEnabled)
            {
                var dev = new MenuItem { Header = "开发者工具" };
                dev.Click += (_, _) => core.OpenDevToolsWindow();
                menu.Items.Add(dev);
            }
        }

        menu.PlacementTarget = contextMenuTarget;
        // 右键菜单就出现在鼠标处，省掉坐标换算
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private static void Populate(
        ContextMenu menu,
        IList<CoreWebView2ContextMenuItem> items,
        CoreWebView2ContextMenuRequestedEventArgs args,
        bool enableDevTools)
    {
        foreach (var item in items)
        {
            if (item.Kind == CoreWebView2ContextMenuItemKind.Separator)
            {
                if (menu.Items.Count > 0 && menu.Items[^1] is not Separator)
                    menu.Items.Add(new Separator());
                continue;
            }

            if (HiddenNames.Contains(item.Name))
                continue;
            if (string.Equals(item.Name, "inspectElement", StringComparison.OrdinalIgnoreCase) && !enableDevTools)
                continue;
            if (item.Kind != CoreWebView2ContextMenuItemKind.Command)
                continue;

            var label = ZhLabels.TryGetValue(item.Name, out var zh) ? zh : item.Label;
            if (string.IsNullOrWhiteSpace(label))
                continue;

            var commandId = item.CommandId;
            var entry = new MenuItem { Header = label, IsEnabled = item.IsEnabled };
            entry.Click += (_, _) => args.SelectedCommandId = commandId;
            menu.Items.Add(entry);
        }

        while (menu.Items.Count > 0 && menu.Items[^1] is Separator)
            menu.Items.RemoveAt(menu.Items.Count - 1);
    }
}