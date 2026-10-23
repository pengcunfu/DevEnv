using System.Windows;
using DevEnv.Core.Services;

namespace DevEnv.Host.Themes;

/// <summary>
/// 主题切换：只替换 App 级资源字典里的调色板（Palette.Light / Palette.Dark），
/// 控件样式（Controls.xaml）用 DynamicResource 取色，无需重建界面。
/// 开关对应 data.json 的 <c>DarkMode</c> 配置项。
/// </summary>
public static class ThemeManager
{
    private const string PaletteLight = "Themes/Palette.Light.xaml";
    private const string PaletteDark = "Themes/Palette.Dark.xaml";

    /// <summary>主题已应用时触发，供后续需要立即刷新外观的控件使用。</summary>
    public static event Action<bool>? ThemeChanged;

    public static bool IsDark { get; private set; }

    /// <summary>启动时按配置应用一次。</summary>
    public static void ApplyFromSettings() => Apply(CoreServices.Settings.GetConfig().DarkMode);

    public static void Apply(bool dark)
    {
        var app = Application.Current;
        if (app is null)
            return;

        var dictionaries = app.Resources.MergedDictionaries;
        var palette = new ResourceDictionary
        {
            Source = new Uri(dark ? PaletteDark : PaletteLight, UriKind.Relative),
        };

        // Controls.xaml 先加载、palette 后加载 → palette 的 key 覆盖同名键
        var index = dictionaries.Count > 0 ? dictionaries.Count - 1 : -1;
        if (index >= 0 && IsPalette(dictionaries[index]))
            dictionaries[index] = palette;
        else
            dictionaries.Add(palette);

        IsDark = dark;
        ThemeChanged?.Invoke(dark);
    }

    private static bool IsPalette(ResourceDictionary dictionary)
    {
        var source = dictionary.Source?.OriginalString;
        return source is not null
            && (source.EndsWith("Palette.Light.xaml", StringComparison.OrdinalIgnoreCase)
                || source.EndsWith("Palette.Dark.xaml", StringComparison.OrdinalIgnoreCase));
    }
}