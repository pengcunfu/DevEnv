using System.IO;
using NAudio.Wave;

namespace DevEnv.Host.SigXor.Toolkit.Voice;

public sealed class AudioCapture : IDisposable
{
    private readonly object _lock = new();
    private readonly Queue<byte[]> _recentBuffers = new();
    private WaveInEvent? _waveIn;
    private List<byte> _completeAudio = [];
    private bool _isRecording;
    private int _sampleRate = 16000;
    private int _channels = 1;
    private int _bitDepth = 16;

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<byte[]>? AudioDataCaptured;

    public bool IsRecording
    {
        get { lock (_lock) return _isRecording; }
    }

    public void StartRecording(int sampleRate = 16000, int channels = 1, int bitDepth = 16)
    {
        lock (_lock)
        {
            if (_isRecording) return;

            try
            {
                DisposeWaveIn();
                _sampleRate = sampleRate;
                _channels = channels;
                _bitDepth = bitDepth;
                _recentBuffers.Clear();
                _completeAudio = [];

                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(sampleRate, bitDepth, channels),
                    BufferMilliseconds = 100,
                };
                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;
                _waveIn.StartRecording();
                _isRecording = true;
                StatusChanged?.Invoke(this, "开始录音...");
            }
            catch (Exception ex)
            {
                _isRecording = false;
                StatusChanged?.Invoke(this, $"录音启动失败：{ex.Message}");
                DisposeWaveIn();
                throw;
            }
        }
    }

    public void StopRecording()
    {
        lock (_lock)
        {
            if (!_isRecording || _waveIn is null) return;
            try
            {
                _waveIn.StopRecording();
                _isRecording = false;
                StatusChanged?.Invoke(this, "停止录音");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"录音停止失败：{ex.Message}");
            }
        }
    }

    public byte[]? GetCompleteAudio()
    {
        lock (_lock)
        {
            if (_completeAudio.Count == 0) return null;
            byte[] result = _completeAudio.ToArray();
            _completeAudio.Clear();
            return result;
        }
    }

    public byte[]? GetLatestAudio()
    {
        lock (_lock)
        {
            return _recentBuffers.Count > 0 ? _recentBuffers.Dequeue() : null;
        }
    }

    public string? SaveToWavFile(string? filePath = null)
    {
        byte[]? audio;
        lock (_lock)
        {
            if (_completeAudio.Count == 0) return null;
            audio = _completeAudio.ToArray();
        }

        if (audio.Length == 0) return null;
        filePath ??= Path.Combine(
            Path.GetTempPath(), $"see_audio_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
        using (var writer = new WaveFileWriter(filePath, new WaveFormat(_sampleRate, _bitDepth, _channels)))
            writer.Write(audio, 0, audio.Length);
        return filePath;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.Buffer.Length == 0) return;

        var buffer = new byte[e.Buffer.Length];
        Array.Copy(e.Buffer, buffer, buffer.Length);

        lock (_lock)
        {
            if (!_isRecording) return;
            _recentBuffers.Enqueue(buffer);
            if (_recentBuffers.Count > 100) _recentBuffers.Dequeue();
            _completeAudio.AddRange(buffer);
        }

        AudioDataCaptured?.Invoke(this, buffer);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        lock (_lock) _isRecording = false;
        StatusChanged?.Invoke(this, "录音已停止");
    }

    private void DisposeWaveIn()
    {
        if (_waveIn is null) return;
        _waveIn.DataAvailable -= OnDataAvailable;
        _waveIn.RecordingStopped -= OnRecordingStopped;
        _waveIn.Dispose();
        _waveIn = null;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            StopRecording();
            DisposeWaveIn();
            _recentBuffers.Clear();
            _completeAudio.Clear();
        }
    }
}
