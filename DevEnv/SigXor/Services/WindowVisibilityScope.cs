using System.Windows;
using DevEnv.Host.SigXor.Views;
using HostWindows = DevEnv.Host.Windows;

namespace DevEnv.Host.SigXor.Services;

public sealed class WindowVisibilityScope : IDisposable
{
    private readonly List<Window> _hidden = [];

    public int HiddenCount => _hidden.Count;

    public static WindowVisibilityScope HideOwnedWindows()
    {
        var scope = new WindowVisibilityScope();
        foreach (Window window in Application.Current.Windows)
        {
            if (!window.IsVisible) continue;
            // 设置窗口已并入宿主设置对话框（DevEnv.Host.Windows.SettingsWindow），
            // 截图前同样要隐藏，否则会被截进画面。
            if (window is not (HostWindows.SettingsWindow or ShellPreviewWindow or LauncherWindow or UpdateWindow or MainWindow))
                continue;

            try
            {
                window.Hide();
                scope._hidden.Add(window);
            }
            catch
            {
            }
        }
        return scope;
    }

    public void Restore()
    {
        foreach (Window window in _hidden)
        {
            try
            {
                window.Show();
            }
            catch
            {
            }
        }
        _hidden.Clear();
    }

    public void Dispose() => Restore();
}
