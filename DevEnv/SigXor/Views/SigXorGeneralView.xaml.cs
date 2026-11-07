using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DevEnv.Host.SigXor.Views;

/// <summary>文件预览的常规选项：字体、备份与更新。</summary>
public partial class SigXorGeneralView : UserControl
{
    private readonly Services.SettingsService _settings;

    public SigXorGeneralView(Services.SettingsService settings)
    {
        InitializeComponent();
        _settings = settings;

        var monoFonts = Fonts.SystemFontFamilies
            .Where(f => IsMonospaced(f))
            .OrderBy(f => f.Source, StringComparer.OrdinalIgnoreCase)
            .ToList();
        FontFamilyCombo.ItemsSource = monoFonts;

        Load();
    }

    private static bool IsMonospaced(FontFamily family)
    {
        var typefaces = family.GetTypefaces();
        if (typefaces.Count == 0) return false;
        if (!typefaces.First().TryGetGlyphTypeface(out var gt)) return false;
        ushort iGlyph = gt.CharacterToGlyphMap.TryGetValue('i', out var ig) ? ig : (ushort)0;
        ushort wGlyph = gt.CharacterToGlyphMap.TryGetValue('W', out var wg) ? wg : (ushort)0;
        return gt.AdvanceWidths[iGlyph] == gt.AdvanceWidths[wGlyph];
    }

    private void Load()
    {
        var s = _settings.Current;
        BackupCheck.IsChecked = s.BackupEnabled;
        CheckUpdatesCheck.IsChecked = s.CheckUpdatesOnStartup;
        FontFamilyCombo.Text = s.TextFontFamily;
        TextFontSizeBox.Text = s.TextFontSize.ToString("0.##", CultureInfo.InvariantCulture);
        HexFontSizeBox.Text = s.HexFontSize.ToString("0.##", CultureInfo.InvariantCulture);
        BytesPerRowBox.Text = s.BytesPerRow.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>把控件上的值写回设置；校验失败返回 false。</summary>
    public bool Save(out string? error)
    {
        error = null;

        string fontFamilyText = FontFamilyCombo.Text.Trim();
        if (string.IsNullOrWhiteSpace(fontFamilyText))
        {
            error = "请输入或选择字体名称。";
            return false;
        }

        if (!double.TryParse(TextFontSizeBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double textFontSize)
            || textFontSize < 8 || textFontSize > 72)
        {
            error = "文本字号需在 8–72 之间。";
            return false;
        }

        if (!double.TryParse(HexFontSizeBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double fontSize)
            || fontSize < 8 || fontSize > 32)
        {
            error = "十六进制字号需在 8–32 之间。";
            return false;
        }

        if (!int.TryParse(BytesPerRowBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bytesPerRow)
            || bytesPerRow < 8 || bytesPerRow > 32)
        {
            error = "每行字节数需在 8–32 之间。";
            return false;
        }

        var s = _settings.Current;
        s.BackupEnabled = BackupCheck.IsChecked == true;
        s.CheckUpdatesOnStartup = CheckUpdatesCheck.IsChecked == true;
        s.TextFontFamily = fontFamilyText;
        s.TextFontSize = textFontSize;
        s.HexFontSize = fontSize;
        s.BytesPerRow = bytesPerRow;
        _settings.Save();
        // 保存了新的默认字号后，不再沿用 Ctrl + 滚轮产生的会话级缩放
        Services.PreviewTextZoom.Reset();
        return true;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!Save(out string? error))
        {
            MessageBox.Show(error ?? "保存失败。", "燧岚开发盒子",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Message.Text = "已保存";
    }
}
