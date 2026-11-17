using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core;
using DevEnv.Core.ColorTools.Models;
using DevEnv.Core.ColorTools.Services;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.ViewModels;

/// <summary>色板中的一格。</summary>
public sealed record ColorSwatch(string Hex, SolidColorBrush Brush);

/// <summary>颜色工具：HEX / RGB / HSL 互转，滑杆调色并生成明暗色板。</summary>
public sealed partial class ColorToolViewModel : ObservableObject
{
    private const string PageId = "color";
    private bool _updating;

    public ColorToolViewModel()
    {
        ApplyColor(new ColorValue(255, 59, 130, 246), updateHexInput: true);
    }

    public ObservableCollection<ColorSwatch> Palette { get; } = [];

    [ObservableProperty]
    private string _hexInput = "#3B82F6";

    [ObservableProperty]
    private int _red = 59;

    [ObservableProperty]
    private int _green = 130;

    [ObservableProperty]
    private int _blue = 246;

    [ObservableProperty]
    private int _alpha = 255;

    [ObservableProperty]
    private string _hexText = "#3B82F6";

    [ObservableProperty]
    private string _rgbText = "rgb(59, 130, 246)";

    [ObservableProperty]
    private string _hslText = "hsl(217, 91%, 60%)";

    [ObservableProperty]
    private SolidColorBrush _previewBrush = Brushes.Transparent;

    [ObservableProperty]
    private string _error = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    partial void OnRedChanged(int value) => OnComponentChanged();

    partial void OnGreenChanged(int value) => OnComponentChanged();

    partial void OnBlueChanged(int value) => OnComponentChanged();

    partial void OnAlphaChanged(int value) => OnComponentChanged();

    [RelayCommand]
    private void ApplyHex()
    {
        if (!ColorToolkit.TryParse(HexInput, out var color, out var error))
        {
            Error = error ?? "无法识别该颜色";
            return;
        }

        ApplyColor(color, updateHexInput: true);
        HostServices.SetStatus($"已应用颜色 {color.Hex}", PageId);
    }

    [RelayCommand]
    private void UseColor(string? hex)
    {
        if (ColorToolkit.TryParse(hex, out var color, out _))
        {
            ApplyColor(color, updateHexInput: true);
            HostServices.SetStatus($"已选择 {color.Hex}", PageId);
        }
    }

    [RelayCommand]
    private void RandomColor()
    {
        var color = new ColorValue(
            (byte)Random.Shared.Next(0, 256),
            (byte)Random.Shared.Next(0, 256),
            (byte)Random.Shared.Next(0, 256),
            (byte)Random.Shared.Next(0, 256));
        ApplyColor(color, updateHexInput: true);
        HostServices.SetStatus($"随机颜色 {color.Hex}", PageId);
    }

    [RelayCommand]
    private void CopyHex()
    {
        HostCallbacks.SetClipboardText?.Invoke(HexText);
        Message = $"已复制 {HexText}";
        HostServices.SetStatus(Message, PageId);
    }

    [RelayCommand]
    private void CopyRgb()
    {
        HostCallbacks.SetClipboardText?.Invoke(RgbText);
        Message = $"已复制 {RgbText}";
        HostServices.SetStatus(Message, PageId);
    }

    [RelayCommand]
    private void CopyHsl()
    {
        HostCallbacks.SetClipboardText?.Invoke(HslText);
        Message = $"已复制 {HslText}";
        HostServices.SetStatus(Message, PageId);
    }

    private void OnComponentChanged()
    {
        if (_updating)
            return;

        ApplyColor(
            new ColorValue(
                (byte)Math.Clamp(Alpha, 0, 255),
                (byte)Math.Clamp(Red, 0, 255),
                (byte)Math.Clamp(Green, 0, 255),
                (byte)Math.Clamp(Blue, 0, 255)),
            updateHexInput: true);
    }

    private void ApplyColor(ColorValue color, bool updateHexInput)
    {
        _updating = true;
        try
        {
            Red = color.R;
            Green = color.G;
            Blue = color.B;
            Alpha = color.A;
            if (updateHexInput)
                HexInput = color.Hex;

            HexText = color.Hex;
            RgbText = color.RgbText;
            HslText = ColorToolkit.HslText(color);
            PreviewBrush = CreateBrush(color);
            Error = string.Empty;
            Message = string.Empty;

            Palette.Clear();
            foreach (var shade in ColorToolkit.GetPalette(color))
                Palette.Add(new ColorSwatch(shade.Hex, CreateBrush(shade)));
        }
        finally
        {
            _updating = false;
        }
    }

    private static SolidColorBrush CreateBrush(ColorValue color)
    {
        var brush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }
}
