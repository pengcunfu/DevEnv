using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevEnv.Core.Input;

namespace DevEnv.Host.Controls;

/// <summary>点击后录制全局组合键的输入框。录制期间自动挂起程序内的全局热键。</summary>
public partial class HotkeyRecorderBox : UserControl
{
    public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
        nameof(Hotkey),
        typeof(string),
        typeof(HotkeyRecorderBox),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnHotkeyPropertyChanged));

    private IDisposable? _captureScope;
    private string _previousHotkey = string.Empty;
    private bool _recording;

    public HotkeyRecorderBox()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewKeyUp += OnPreviewKeyUp;
        LostKeyboardFocus += OnLostKeyboardFocus;
        Unloaded += (_, _) => CancelCapture();
        UpdateDisplay();
    }

    public string Hotkey
    {
        get => (string)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    public bool IsCapturing => _recording;

    public event EventHandler? HotkeyChanged;

    private static void OnHotkeyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((HotkeyRecorderBox)d).UpdateDisplay();

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        BeginCapture();
        e.Handled = true;
    }

    public void BeginCapture()
    {
        if (_recording) return;

        _previousHotkey = Hotkey;
        _recording = true;
        HintText.Visibility = Visibility.Collapsed;
        // 必须先挂起全局热键，再抢焦点，避免旧组合键在录制开始前触发
        _captureScope = HotkeyCaptureState.Begin();
        UpdateDisplay();
        Focus();
        Keyboard.Focus(this);
    }

    public void CancelCapture()
    {
        if (!_recording) return;

        _recording = false;
        _captureScope?.Dispose();
        _captureScope = null;
        Hotkey = _previousHotkey;
        UpdateDisplay();
    }

    private void EndCapture()
    {
        _recording = false;
        _captureScope?.Dispose();
        _captureScope = null;
        UpdateDisplay();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_recording) return;

        e.Handled = true;
        HintText.Visibility = Visibility.Collapsed;

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            CancelCapture();
            return;
        }

        if (IsModifierKey(key))
        {
            UpdateDisplay();
            return;
        }

        int virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey == 0)
            return;

        var gesture = new HotkeyGesture(GetModifiers(), virtualKey);
        HotkeyValidationResult validation = HotkeyValidator.Validate(gesture);
        if (!validation.IsValid)
        {
            ShowHint(validation.Message ?? "快捷键无效。");
            return;
        }

        Hotkey = HotkeyParser.FormatCanonical(gesture);
        EndCapture();
        HotkeyChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (!_recording) return;
        e.Handled = true;
        UpdateDisplay();
    }

    private void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        CancelCapture();
    }

    private HotkeyModifiers GetModifiers()
    {
        HotkeyModifiers modifiers = HotkeyModifiers.None;
        ModifierKeys current = Keyboard.Modifiers;
        if (current.HasFlag(ModifierKeys.Control)) modifiers |= HotkeyModifiers.Control;
        if (current.HasFlag(ModifierKeys.Alt)) modifiers |= HotkeyModifiers.Alt;
        if (current.HasFlag(ModifierKeys.Shift)) modifiers |= HotkeyModifiers.Shift;
        if (current.HasFlag(ModifierKeys.Windows) || IsWinKeyDown()) modifiers |= HotkeyModifiers.Windows;
        return modifiers;
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or
        Key.LWin or Key.RWin;

    private void ShowHint(string message)
    {
        HintText.Text = message;
        HintText.Visibility = Visibility.Visible;
    }

    private void UpdateDisplay()
    {
        if (_recording)
        {
            UpdateRecordingDisplay();
            return;
        }

        ValueText.Text = string.IsNullOrWhiteSpace(Hotkey) ? "未设置" : Hotkey;
        if (TryFindResource("TextPrimaryBrush") is System.Windows.Media.Brush textBrush)
            ValueText.Foreground = textBrush;
        if (TryFindResource("CardBorderBrush") is System.Windows.Media.Brush borderBrush)
            RootBorder.BorderBrush = borderBrush;
    }

    private void UpdateRecordingDisplay()
    {
        HotkeyModifiers modifiers = GetModifiers();
        if (modifiers == HotkeyModifiers.None)
        {
            ValueText.Text = "请按下组合键…（Esc 取消）";
        }
        else
        {
            var parts = new List<string>(4);
            if (modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
            if (modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
            if (modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
            if (modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");
            ValueText.Text = string.Join('+', parts) + "+…";
        }

        if (TryFindResource("AccentBrush") is System.Windows.Media.Brush accentBrush)
        {
            ValueText.Foreground = accentBrush;
            RootBorder.BorderBrush = accentBrush;
        }
    }

    private static bool IsWinKeyDown() =>
        (GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
