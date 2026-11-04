namespace DevEnv.Host.SigXor;

public sealed class VoiceSettings
{
    public string Shortcut { get; set; } = "right-alt";
    public double HoldThresholdSeconds { get; set; } = 0.4;
    public string Engine { get; set; } = "sensevoice";
    public string Language { get; set; } = "auto";
    public bool UseClipboard { get; set; } = true;
    public double TypingDelay { get; set; } = 0.05;
    public int SampleRate { get; set; } = 16000;
    public int Channels { get; set; } = 1;
    public int BitDepth { get; set; } = 16;
    public bool AutoStartService { get; set; } = true;
    public bool DebugMode { get; set; }
    public bool SaveAudioFiles { get; set; }
}

public sealed class CaptureSettings
{
    public bool Enabled { get; set; } = true;
    public string Modifier { get; set; } = "alt";
    public bool AllowBareBacktick { get; set; }
}

public sealed class UiSettings
{
    public bool ShowNotifications { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool SilentStart { get; set; } = true;
}
