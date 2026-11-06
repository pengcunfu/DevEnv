using System.IO;
using System.Runtime.InteropServices;
using NAudio.Wave;
using DevEnv.Host.SigXor.Services;
using SherpaOnnx;

namespace DevEnv.Host.SigXor.Toolkit.Voice;

public interface ISpeechEngine : IDisposable
{
    event EventHandler<string>? StatusChanged;
    event EventHandler<Exception>? Error;

    bool IsInitialized { get; }
    string EngineName { get; }

    Task<bool> InitializeAsync();
    Task<string?> RecognizeFromBufferAsync(byte[] audioBuffer, int sampleRate = 16000);
}

public sealed class SpeechRecognizer : IDisposable
{
    private readonly SettingsService _settings;
    private ISpeechEngine? _engine;

    public SpeechRecognizer(SettingsService settings) => _settings = settings;

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<Exception>? Error;

    public bool IsInitialized => _engine?.IsInitialized ?? false;
    public string EngineName => _engine?.EngineName ?? "未初始化";

    public async Task<bool> InitializeAsync()
    {
        try
        {
            DisposeEngine();
            _engine = new SenseVoiceEngine(_settings);
            _engine.StatusChanged += (_, message) => StatusChanged?.Invoke(this, message);
            _engine.Error += (_, error) => Error?.Invoke(this, error);
            StatusChanged?.Invoke(this, $"{_engine.EngineName} 语音识别引擎初始化中...");
            return await _engine.InitializeAsync();
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, ex);
            return false;
        }
    }

    public Task<string?> RecognizeFromBufferAsync(byte[] audioBuffer, int sampleRate = 16000)
    {
        if (_engine is null)
            throw new InvalidOperationException("语音识别引擎尚未初始化");
        return _engine.RecognizeFromBufferAsync(audioBuffer, sampleRate);
    }

    private void DisposeEngine()
    {
        _engine?.Dispose();
        _engine = null;
    }

    public void Dispose() => DisposeEngine();
}

public sealed class SenseVoiceEngine : ISpeechEngine
{
    // sherpa-onnx 的托管封装在构造失败时对象仍然存在，GC 会执行
    // Finalize → Cleanup → SherpaOnnxDestroyOfflineRecognizer。如果原生库本身
    // 加载不了，析构里的 P/Invoke 会再抛一次 DllNotFoundException；finalizer
    // 线程上的未处理异常会直接终止进程（退出码 0xE0434352），表现为程序启动后
    // 无声退出。所以先探测原生库，加载失败就根本不要构造 OfflineRecognizer。
    private static IntPtr s_nativeHandle;

    private readonly SettingsService _settings;
    private readonly object _lock = new();
    private OfflineRecognizer? _recognizer;
    private bool _isInitialized;

    public SenseVoiceEngine(SettingsService settings) => _settings = settings;

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<Exception>? Error;

    public string EngineName => "SenseVoice";
    public bool IsInitialized => _isInitialized;

    public async Task<bool> InitializeAsync()
    {
        if (_isInitialized) return true;

        try
        {
            string? dir = SpeechModelManager.ResolveModelDirectory();
            if (dir is null)
            {
                Error?.Invoke(this, new InvalidOperationException("SenseVoice 模型未下载"));
                return false;
            }

            string model = Path.Combine(dir, SpeechModelManager.ModelFileName);
            string tokens = Path.Combine(dir, SpeechModelManager.TokensFileName);
            StatusChanged?.Invoke(this, "正在初始化 SenseVoice 引擎...");

            if (!EnsureNativeLibrary())
            {
                Error?.Invoke(this, new InvalidOperationException(
                    "无法加载 sherpa-onnx 原生库，语音识别不可用。请安装 Microsoft Visual C++ 2015-2022 运行库（x64）后重试。"));
                return false;
            }

            await Task.Run(() =>
            {
                lock (_lock)
                {
                    var config = BuildConfig(model, tokens);
                    _recognizer = new OfflineRecognizer(config);
                    _isInitialized = true;
                }
            });

            StatusChanged?.Invoke(this, "SenseVoice 引擎初始化完成");
            return true;
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, new Exception($"SenseVoice 初始化失败：{ex.Message}", ex));
            return false;
        }
    }

    private static bool EnsureNativeLibrary()
    {
        if (s_nativeHandle != IntPtr.Zero) return true;

        // 走与 DllImport 相同的程序集上下文探测，才能找到 deps.json 里
        // runtimes/win-x64/native 下的原生库；失败时只返回 false，不抛异常。
        if (!NativeLibrary.TryLoad("sherpa-onnx-c-api", typeof(OfflineRecognizer).Assembly, null, out IntPtr handle))
            return false;

        // 句柄一直持有到进程结束，避免库被卸载后又触发 P/Invoke
        s_nativeHandle = handle;
        return true;
    }

    private OfflineRecognizerConfig BuildConfig(string model, string tokens)
    {
        VoiceSettings voice = _settings.Current.Voice;
        var config = new OfflineRecognizerConfig();
        config.FeatConfig.SampleRate = voice.SampleRate;
        config.FeatConfig.FeatureDim = 80;
        config.ModelConfig.Tokens = tokens;
        config.ModelConfig.NumThreads = Math.Max(1, Environment.ProcessorCount / 2);
        config.ModelConfig.Provider = "cpu";
        config.ModelConfig.Debug = 0;
        config.ModelConfig.SenseVoice.Model = model;
        config.ModelConfig.SenseVoice.Language = MapLanguage(voice.Language);
        config.ModelConfig.SenseVoice.UseInverseTextNormalization = 1;
        config.DecodingMethod = "greedy_search";
        return config;
    }

    private static string MapLanguage(string language) =>
        language switch
        {
            "zh-CN" or "zh" => "zh",
            "en-US" or "en" => "en",
            "ja" => "ja",
            "ko" => "ko",
            "yue" => "yue",
            _ => "auto",
        };

    public async Task<string?> RecognizeFromBufferAsync(byte[] audioBuffer, int sampleRate = 16000)
    {
        if (audioBuffer.Length == 0) return null;
        if (!_isInitialized)
        {
            Error?.Invoke(this, new InvalidOperationException("SenseVoice 引擎未初始化"));
            return null;
        }

        string tempFile = Path.Combine(Path.GetTempPath(), $"see_voice_{Guid.NewGuid():N}.wav");
        try
        {
            using (var writer = new WaveFileWriter(tempFile, new WaveFormat(sampleRate, 16, 1)))
                writer.Write(audioBuffer, 0, audioBuffer.Length);

            return await Task.Run(() => RecognizeFile(tempFile));
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
        }
    }

    private string? RecognizeFile(string wavPath)
    {
        lock (_lock)
        {
            if (_recognizer is null) return null;
            try
            {
                StatusChanged?.Invoke(this, "正在识别语音...");
                using OfflineStream stream = _recognizer.CreateStream();
                var (rate, samples) = LoadWavSamples(wavPath);
                stream.AcceptWaveform(rate, samples);
                _recognizer.Decode(stream);
                string? text = stream.Result.Text?.Trim();
                StatusChanged?.Invoke(this, "语音识别完成");
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new Exception($"语音识别失败：{ex.Message}", ex));
                return null;
            }
        }
    }

    private static (int SampleRate, float[] Samples) LoadWavSamples(string path)
    {
        using var reader = new AudioFileReader(path);
        var buffer = new float[4096];
        var samples = new List<float>();
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            samples.AddRange(buffer.Take(read));
        return ((int)reader.WaveFormat.SampleRate, samples.ToArray());
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_recognizer is IDisposable disposable) disposable.Dispose();
            _recognizer = null;
            _isInitialized = false;
        }
    }
}
