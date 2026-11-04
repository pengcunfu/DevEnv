using System.Windows;
using DevEnv.Host.SigXor.Views;

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
            if (window is not (SettingsWindow or ShellPreviewWindow or LauncherWindow or AboutWindow or UpdateWindow or MainWindow))
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
