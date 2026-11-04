using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DevEnv.Core.SigXor.Toolkit;
using DevEnv.Host.SigXor.Services;
using Forms = System.Windows.Forms;

namespace DevEnv.Host.SigXor.Toolkit.Voice;

public partial class VoiceOverlayWindow : Window
{
    private readonly Border[] _bars;
    private readonly DispatcherTimer _waveTimer;
    private readonly Random _random = new();
    private readonly WpfScreenService _screens;
    private Forms.Screen? _targetScreen;

    public VoiceOverlayWindow(WpfScreenService screens)
    {
        _screens = screens;
        InitializeComponent();
        _bars = [Bar1, Bar2, Bar3, Bar4];
        _waveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(90) };
        _waveTimer.Tick += OnWaveTick;
        SourceInitialized += (_, _) =>
        {
            WindowPlacement.SetNoActivate(this);
            Reposition();
        };
        Loaded += (_, _) => Reposition();
    }

    public Forms.Screen? TargetScreen => _targetScreen;

    public void SetTargetScreen(Forms.Screen screen)
    {
        _targetScreen = screen;
        if (IsVisible) Reposition();
    }

    public void ShowRecording()
    {
        StatusText.Text = "SigXor 聆听中";
        ShowOverlay();
        _waveTimer.Start();
    }

    public void ShowProcessing()
    {
        StatusText.Text = "识别中";
        ShowOverlay();
        _waveTimer.Start();
    }

    public void HideOverlay()
    {
        _waveTimer.Stop();
        Hide();
    }

    private void ShowOverlay()
    {
        if (!IsVisible) Show();
        Reposition();
        Topmost = true;
    }

    private void Reposition()
    {
        var screen = _targetScreen ?? Forms.Screen.PrimaryScreen;
        if (screen is null) return;

        PixelRect area = _screens.GetWorkingArea(screen);
        double scale = _screens.GetScale(screen);
        int width = PixelRect.DipToPhysical(190, scale);
        int height = PixelRect.DipToPhysical(52, scale);
        int margin = PixelRect.DipToPhysical(28, scale);
        int x = area.X + (area.Width - width) / 2;
        int y = area.Bottom - height - margin;
        WindowPlacement.SetPhysicalBounds(this, new PixelRect(x, y, width, height));
    }

    private void OnWaveTick(object? sender, EventArgs e)
    {
        foreach (Border bar in _bars)
            bar.Height = _random.Next(5, 20);
    }
}
