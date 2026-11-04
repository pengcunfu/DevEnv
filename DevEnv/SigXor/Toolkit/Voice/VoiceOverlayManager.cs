using DevEnv.Host.SigXor.Services;
using Forms = System.Windows.Forms;

namespace DevEnv.Host.SigXor.Toolkit.Voice;

public sealed class VoiceOverlayManager : IDisposable
{
    private readonly WpfScreenService _screens;
    private readonly List<VoiceOverlayWindow> _overlays = [];
    private bool _disposed;

    public VoiceOverlayManager(WpfScreenService screens) => _screens = screens;

    public void ShowRecording()
    {
        Sync();
        foreach (VoiceOverlayWindow overlay in _overlays)
            overlay.ShowRecording();
    }

    public void ShowProcessing()
    {
        Sync();
        foreach (VoiceOverlayWindow overlay in _overlays)
            overlay.ShowProcessing();
    }

    public void HideAll()
    {
        foreach (VoiceOverlayWindow overlay in _overlays)
            overlay.HideOverlay();
    }

    private void Sync()
    {
        if (_disposed) return;

        Forms.Screen[] screens = Forms.Screen.AllScreens;
        for (int i = _overlays.Count - 1; i >= 0; i--)
        {
            VoiceOverlayWindow overlay = _overlays[i];
            bool alive = screens.Any(s => SameScreen(s, overlay.TargetScreen));
            if (alive) continue;
            overlay.Close();
            _overlays.RemoveAt(i);
        }

        foreach (Forms.Screen screen in screens)
        {
            VoiceOverlayWindow? existing = _overlays.FirstOrDefault(o => SameScreen(o.TargetScreen, screen));
            if (existing is not null)
            {
                existing.SetTargetScreen(screen);
                continue;
            }

            var overlay = new VoiceOverlayWindow(_screens);
            overlay.SetTargetScreen(screen);
            _overlays.Add(overlay);
        }
    }

    private static bool SameScreen(Forms.Screen? a, Forms.Screen? b) =>
        a is not null && b is not null && a.Bounds == b.Bounds && a.DeviceName == b.DeviceName;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (VoiceOverlayWindow overlay in _overlays)
        {
            try { overlay.Close(); } catch { }
        }
        _overlays.Clear();
    }
}
