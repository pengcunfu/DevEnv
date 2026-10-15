using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace ClipboardApp.Views;

public partial class HotkeySettingsWindow : Window
{
    public List<string> Modifiers { get; private set; }
    public string HotkeyKey { get; private set; }
    public bool Confirmed { get; private set; }

    public HotkeySettingsWindow() : this(["ctrl"], "space")
    {
    }

    public HotkeySettingsWindow(IEnumerable<string> modifiers, string key)
    {
        Modifiers = modifiers.ToList();
        HotkeyKey = key;
        InitializeComponent();
        UpdatePreview();
        Opened += (_, _) => HotkeyBox.Focus();
    }

    private void HotkeyBox_OnKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        e.Handled = true;
        if (e.Key is Key.Escape)
            return;

        var mods = new List<string>();
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            mods.Add("ctrl");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            mods.Add("alt");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            mods.Add("shift");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta))
            mods.Add("win");

        var key = MapKey(e.Key);
        if (key is null || mods.Count == 0)
            return;

        Modifiers = mods;
        HotkeyKey = key;
        UpdatePreview();
    }

    private static string? MapKey(Key key) => key switch
    {
        Key.Space => "space",
        Key.Escape => "esc",
        >= Key.A and <= Key.Z => ((char)('a' + (key - Key.A))).ToString(),
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.F1 and <= Key.F12 => $"f{1 + (key - Key.F1)}",
        _ => null,
    };

    private void UpdatePreview()
    {
        if (Modifiers.Count == 0 || string.IsNullOrEmpty(HotkeyKey))
        {
            HotkeyBox.Text = string.Empty;
            PreviewLabel.Text = "尚未设置快捷键";
            PreviewLabel.Foreground = Brushes.Gray;
            return;
        }

        var text = string.Join('+', Modifiers.Select(m => m.ToUpperInvariant()).Append(HotkeyKey.ToUpperInvariant()));
        HotkeyBox.Text = text;
        PreviewLabel.Text = $"当前: {text}";
        PreviewLabel.Foreground = Brushes.Green;
    }

    private void ClearButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Modifiers = [];
        HotkeyKey = string.Empty;
        UpdatePreview();
    }

    private void OkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e) => Close();
}
