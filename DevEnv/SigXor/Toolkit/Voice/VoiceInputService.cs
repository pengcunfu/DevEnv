using System.IO;
using System.Windows.Threading;
using DevEnv.Core.Components;
using DevEnv.Core.SigXor.Toolkit;
using CoreServices = DevEnv.Core.Services.CoreServices;
using DevEnv.Host.SigXor.Services;
using DevEnv.Host.SigXor.Toolkit.Input;

namespace DevEnv.Host.SigXor.Toolkit.Voice;

public sealed class VoiceInputService : IDisposable
{
    private enum RecordingTrigger
    {
        None,
        KeyboardToggle,
        KeyboardHold,
    }

    private readonly SettingsService _settings;
    private readonly Dispatcher _dispatcher;
    private readonly AudioCapture _audio = new();
    private readonly TextInjector _text = new();
    private readonly VoiceOverlayManager _overlay;
    private readonly IDisposable _hotkeyRegistration;
    private readonly DispatcherTimer _holdTimer;
    private SpeechRecognizer? _recognizer;
    private VoiceServiceStatus _status = new(false, false, false, "未启动");
    private bool _isRunning;
    private bool _isRecording;
    private bool _isProcessing;
    private bool _isShortcutDown;
    private bool _holdTriggered;
    private bool _missingModelNotified;
    private bool _disposed;
    private RecordingTrigger _trigger;

    public VoiceInputService(
        SettingsService settings,
        GlobalHotkeyRouter router,
        WpfScreenService screens,
        Dispatcher dispatcher)
    {
        _settings = settings;
        _dispatcher = dispatcher;
        _overlay = new VoiceOverlayManager(screens);
        _holdTimer = new DispatcherTimer();
        _holdTimer.Tick += OnHoldTick;
        _hotkeyRegistration = router.Register(200, OnVoiceKeyDown, OnVoiceKeyUp);
    }

    public event EventHandler<VoiceServiceStatus>? StatusChanged;
    public event Action<string, string>? NotificationRequested;

    public bool IsRunning => _isRunning;
    public bool IsRecording => _isRecording;
    public bool IsProcessing => _isProcessing;
    public VoiceServiceStatus Status => _status;

    public async Task<bool> StartServiceAsync(bool notify = true)
    {
        if (_disposed) return false;
        if (_isRunning) return true;

        if (!CoreServices.Components.IsInstalled(ComponentCatalog.Voice))
        {
            SetStatus(false, false, "引擎组件未安装");
            if (notify) Notify("语音输入", "语音引擎组件未安装，请在「服务与模型」中下载（约 8 MB，含 onnxruntime）。");
            return false;
        }

        if (!SpeechModelManager.IsInstalled)
        {
            SetStatus(false, false, "模型未下载");
            if (notify) Notify("语音输入", "SenseVoice 模型未下载，请在「服务与模型」中下载。");
            return false;
        }

        SetStatus(false, false, "正在初始化语音引擎...");
        try
        {
            _recognizer?.Dispose();
            _recognizer = new SpeechRecognizer(_settings);
            _recognizer.StatusChanged += OnRecognizerStatus;
            _recognizer.Error += OnRecognizerError;

            bool initialized = await _recognizer.InitializeAsync();
            if (!initialized)
            {
                _recognizer.Dispose();
                _recognizer = null;
                SetStatus(false, false, "初始化失败");
                if (notify) Notify("语音输入", "语音引擎初始化失败，请检查模型文件或重新下载。");
                return false;
            }

            _isRunning = true;
            _missingModelNotified = false;
            string shortcut = ShortcutDefinition.FormatVoiceShortcut(_settings.Current.Voice.Shortcut);
            SetStatus(false, false, $"就绪（{shortcut}）");
            DebugLog($"服务启动，快捷键 {shortcut}");
            if (notify) Notify("语音服务已启动", $"使用{shortcut}进行语音输入。");
            return true;
        }
        catch (Exception ex)
        {
            _recognizer?.Dispose();
            _recognizer = null;
            SetStatus(false, false, "启动失败");
            if (notify) Notify("语音服务启动失败", ex.Message);
            return false;
        }
    }

    public async Task TryAutoStartAsync()
    {
        if (!_settings.Current.Voice.AutoStartService) return;
        if (!CoreServices.Components.IsInstalled(ComponentCatalog.Voice))
        {
            SetStatus(false, false, "引擎组件未安装");
            return;
        }
        if (!SpeechModelManager.IsInstalled)
        {
            SetStatus(false, false, "模型未下载");
            return;
        }

        try { await StartServiceAsync(notify: false); }
        catch { }
    }

    public void StopService(bool notify = true)
    {
        if (_disposed) return;
        if (!_isRunning && !_isRecording && !_isProcessing)
        {
            SetStatus(false, false, "未启动");
            return;
        }

        _holdTimer.Stop();
        _isShortcutDown = false;
        _holdTriggered = false;

        if (_isRecording)
        {
            _isRecording = false;
            _trigger = RecordingTrigger.None;
            _audio.StopRecording();
        }

        _overlay.HideAll();
        _isRunning = false;

        if (!_isProcessing)
        {
            _recognizer?.Dispose();
            _recognizer = null;
        }

        SetStatus(false, _isProcessing, _isProcessing ? "正在结束识别..." : "已停止");
        if (notify) Notify("语音服务已停止", "语音输入功能已关闭。");
    }

    private bool OnVoiceKeyDown(int vkCode)
    {
        if (vkCode != ShortcutDefinition.ToVoiceVirtualKey(_settings.Current.Voice.Shortcut))
            return false;

        bool suppress = IsCapsLockShortcut();
        if (_disposed) return suppress;

        if (!_isRunning)
        {
            if (!_missingModelNotified)
            {
                _missingModelNotified = true;
                Notify("语音输入",
                    !CoreServices.Components.IsInstalled(ComponentCatalog.Voice)
                        ? "语音引擎组件未安装，请在「服务与模型」中下载（约 8 MB）。"
                        : SpeechModelManager.IsInstalled
                            ? "语音服务未启动，请在「语音与截屏」中启动服务。"
                            : "SenseVoice 模型未下载，请在「服务与模型」中下载。");
            }
            return suppress;
        }

        if (_isProcessing || _isShortcutDown) return suppress;

        _isShortcutDown = true;
        _holdTriggered = false;
        _text.CaptureTargetWindow();
        _holdTimer.Stop();
        _holdTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(
            _settings.Current.Voice.HoldThresholdSeconds, 0.1, 5));
        _holdTimer.Start();
        SetStatus(_isRecording, false, "按键已按下");
        return suppress;
    }

    private bool OnVoiceKeyUp(int vkCode)
    {
        if (vkCode != ShortcutDefinition.ToVoiceVirtualKey(_settings.Current.Voice.Shortcut))
            return false;

        bool suppress = IsCapsLockShortcut();
        if (!_isShortcutDown) return suppress;

        _isShortcutDown = false;
        _holdTimer.Stop();

        if (_holdTriggered)
        {
            _holdTriggered = false;
            if (_isRecording && _trigger == RecordingTrigger.KeyboardHold)
                _ = StopRecordingAsync();
        }
        else if (_isRecording && _trigger == RecordingTrigger.KeyboardToggle)
        {
            _ = StopRecordingAsync();
        }
        else if (!_isRecording && !_isProcessing && _isRunning)
        {
            StartRecording(RecordingTrigger.KeyboardToggle);
        }

        return suppress;
    }

    private void OnHoldTick(object? sender, EventArgs e)
    {
        _holdTimer.Stop();
        if (!_isShortcutDown || !_isRunning || _isRecording || _isProcessing) return;
        _holdTriggered = true;
        StartRecording(RecordingTrigger.KeyboardHold);
    }

    private void StartRecording(RecordingTrigger trigger)
    {
        if (!_isRunning || _isRecording || _isProcessing) return;

        VoiceSettings voice = _settings.Current.Voice;
        try
        {
            _audio.StartRecording(voice.SampleRate, voice.Channels, voice.BitDepth);
            _trigger = trigger;
            _isRecording = true;
            try { _overlay.ShowRecording(); } catch { }
            SetStatus(true, false, "正在录音...");
            DebugLog($"开始录音（{trigger}）");
        }
        catch (Exception ex)
        {
            try { _audio.StopRecording(); } catch { }
            _trigger = RecordingTrigger.None;
            _isRecording = false;
            _overlay.HideAll();
            SetStatus(false, false, "录音启动失败");
            Notify("录音启动失败", ex.Message);
        }
    }

    private async Task StopRecordingAsync()
    {
        if (!_isRecording) return;

        _isRecording = false;
        _trigger = RecordingTrigger.None;
        _audio.StopRecording();
        _isProcessing = true;
        try { _overlay.ShowProcessing(); } catch { }
        SetStatus(false, true, "正在识别...");

        try
        {
            await Task.Delay(120);
            if (_settings.Current.Voice.SaveAudioFiles)
                SaveDebugAudio();

            byte[]? audio = _audio.GetCompleteAudio();
            if (audio is null || audio.Length < 3200)
            {
                SetStatus(false, false, "未识别到语音");
                Notify("语音输入", "未识别到语音，请重试。");
                return;
            }

            SpeechRecognizer? recognizer = _recognizer;
            if (recognizer is null)
            {
                SetStatus(false, false, "语音引擎未就绪");
                return;
            }

            string? text = await recognizer.RecognizeFromBufferAsync(
                audio, _settings.Current.Voice.SampleRate);

            if (!_isRunning)
            {
                SetStatus(false, false, "已停止");
                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                SetStatus(false, false, "未识别到文字");
                Notify("语音输入", "未识别到文字，请重试。");
                return;
            }

            await InsertTextAsync(text);
            DebugLog($"识别完成：{text}");
            SetStatus(false, false, $"就绪（{ShortcutDefinition.FormatVoiceShortcut(_settings.Current.Voice.Shortcut)}）");
        }
        catch (Exception ex)
        {
            SetStatus(false, false, "识别失败");
            Notify("语音识别失败", ex.Message);
        }
        finally
        {
            _isProcessing = false;
            _overlay.HideAll();

            if (!_isRunning && _recognizer is not null)
            {
                _recognizer.Dispose();
                _recognizer = null;
            }
        }
    }

    private async Task InsertTextAsync(string text)
    {
        VoiceSettings voice = _settings.Current.Voice;
        if (voice.UseClipboard)
            await _text.InsertTextAsync(text);
        else
            await _text.TypeTextAsync(text, voice.TypingDelay);

        string preview = text.Length > 80 ? text[..80] + "…" : text;
        Notify("文字输入完成", preview);
    }

    private void DebugLog(string message)
    {
        if (!_settings.Current.Voice.DebugMode) return;
        try
        {
            Directory.CreateDirectory(AppPaths.LogDirectory);
            File.AppendAllText(
                Path.Combine(AppPaths.LogDirectory, "voice.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private void SaveDebugAudio()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.AudioDirectory);
            string path = Path.Combine(
                AppPaths.AudioDirectory, $"voice_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
            _audio.SaveToWavFile(path);
        }
        catch
        {
        }
    }

    private bool IsCapsLockShortcut() =>
        ShortcutDefinition.NormalizeVoiceShortcut(_settings.Current.Voice.Shortcut)
            == ShortcutDefinition.CapsLock;

    private void OnRecognizerStatus(object? sender, string message)
    {
        if (_disposed) return;
        try { _dispatcher.BeginInvoke(() => SetStatus(_isRecording, _isProcessing, message)); }
        catch { }
    }

    private void OnRecognizerError(object? sender, Exception error)
    {
        if (_disposed) return;
        try
        {
            _dispatcher.BeginInvoke(() =>
            {
                if (_disposed) return;
                SetStatus(_isRecording, _isProcessing, "识别失败");
                Notify("语音识别错误", error.Message);
            });
        }
        catch { }
    }

    private void SetStatus(bool recording, bool processing, string message)
    {
        _status = new VoiceServiceStatus(_isRunning, recording, processing, message);
        StatusChanged?.Invoke(this, _status);
    }

    private void Notify(string title, string message)
    {
        if (_settings.Current.Ui.ShowNotifications)
            NotificationRequested?.Invoke(title, message);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _hotkeyRegistration.Dispose();
        StopService(notify: false);
        _disposed = true;
        _holdTimer.Stop();
        _audio.Dispose();
        _overlay.Dispose();
        if (!_isProcessing)
        {
            _recognizer?.Dispose();
            _recognizer = null;
        }
    }
}
