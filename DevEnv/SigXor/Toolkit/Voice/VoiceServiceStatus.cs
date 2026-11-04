namespace DevEnv.Host.SigXor.Toolkit.Voice;

public sealed record VoiceServiceStatus(
    bool IsRunning,
    bool IsRecording,
    bool IsProcessing,
    string Message);
