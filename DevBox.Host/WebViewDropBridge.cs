using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DevBox.Host;

/// <summary>
/// WebView2 占满客户区时 Form.DragDrop 收不到文件；用页面 drop + postMessageWithAdditionalObjects
/// 把本地路径交给宿主（CoreWebView2File.Path）。文件夹拖入时 FileList 常为空，改走 file:// 导航拦截。
/// </summary>
internal static class WebViewDropBridge
{
    private const string DropScript = """
        (() => {
          if (window.__devboxDropHooked) return;
          window.__devboxDropHooked = true;

          const blockNav = (e) => {
            e.preventDefault();
            e.stopPropagation();
          };

          window.addEventListener('dragenter', blockNav, true);
          window.addEventListener('dragover', (e) => {
            blockNav(e);
            if (e.dataTransfer) e.dataTransfer.dropEffect = 'copy';
          }, true);

          window.addEventListener('drop', (e) => {
            try {
              const files = Array.from(e.dataTransfer?.files || []);
              const wv = window.chrome?.webview;
              if (files.length && wv?.postMessageWithAdditionalObjects) {
                blockNav(e);
                wv.postMessageWithAdditionalObjects({ type: 'devbox-file-drop' }, files);
                return;
              }
              if (files.length && wv?.postMessage) {
                blockNav(e);
                wv.postMessage({ type: 'devbox-file-drop-unsupported' });
                return;
              }
              // FileList 为空（常见于拖文件夹）：不拦截，让 WebView 走 file://，由宿主 NavigationStarting 取路径
            } catch (_) { /* ignore */ }
          }, true);
        })();
        """;

    public static async Task AttachAsync(WebView2 webView, Func<IReadOnlyList<string>, Task> onPathsDropped)
    {
        var core = webView.CoreWebView2
            ?? throw new InvalidOperationException("CoreWebView2 尚未初始化");

        // 必须允许外部拖入，否则页面 drop 事件都不会带本地 File
        webView.AllowExternalDrop = true;

        await core.AddScriptToExecuteOnDocumentCreatedAsync(DropScript);

        core.WebMessageReceived += (_, e) =>
        {
            string? type = null;
            try
            {
                using var doc = JsonDocument.Parse(e.WebMessageAsJson);
                if (doc.RootElement.TryGetProperty("type", out var t))
                    type = t.GetString();
            }
            catch
            {
                return;
            }

            if (string.Equals(type, "devbox-file-drop-unsupported", StringComparison.Ordinal))
            {
                MessageBox.Show(
                    "当前 WebView2 运行时过旧，无法读取拖入文件的本地路径。\n请更新 Edge WebView2 Runtime。",
                    "程序盒子",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (!string.Equals(type, "devbox-file-drop", StringComparison.Ordinal))
                return;

            var paths = new List<string>();
            try
            {
                foreach (var obj in e.AdditionalObjects)
                {
                    if (obj is CoreWebView2File file && !string.IsNullOrWhiteSpace(file.Path))
                        paths.Add(file.Path);
                }
            }
            catch
            {
                // AdditionalObjects 在部分环境下可能抛错
            }

            if (paths.Count == 0)
                return;

            _ = onPathsDropped(paths);
        };

        // 文件夹 / 未走 AdditionalObjects 时：拖入会触发 file:// 导航或新窗口
        core.NavigationStarting += (_, e) =>
        {
            if (!TryGetLocalPath(e.Uri, out var path))
                return;
            e.Cancel = true;
            _ = onPathsDropped(new[] { path });
        };

        core.NewWindowRequested += (_, e) =>
        {
            if (!TryGetLocalPath(e.Uri, out var path))
                return;
            e.Handled = true;
            _ = onPathsDropped(new[] { path });
        };
    }

    private static bool TryGetLocalPath(string? uri, out string path)
    {
        path = "";
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var u) || !u.IsFile)
            return false;
        path = Uri.UnescapeDataString(u.LocalPath);
        return !string.IsNullOrWhiteSpace(path);
    }
}
