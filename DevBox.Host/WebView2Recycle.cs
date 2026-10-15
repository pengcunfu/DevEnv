using Microsoft.Web.WebView2.WinForms;

namespace DevBox.Host;

/// <summary>销毁并重建 WebView2 控件，释放对应 renderer / 相关子进程。</summary>
internal static class WebView2Recycle
{
    public static WebView2 Replace(Control parent, WebView2 current)
    {
        parent.Controls.Remove(current);
        try
        {
            current.Dispose();
        }
        catch
        {
            // ignore dispose races during shutdown
        }

        var next = new WebView2 { Dock = DockStyle.Fill };
        parent.Controls.Add(next);
        next.BringToFront();
        return next;
    }
}
