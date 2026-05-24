using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace DevEnv.Services;

public static class UiIcons
{
    public static IImage? UacShield { get; } = LoadShieldIcon();

    private static IImage? LoadShieldIcon()
    {
        try
        {
            var uri = new Uri("avares://DevEnv/Resources/icon.png");
            return new Bitmap(AssetLoader.Open(uri));
        }
        catch
        {
            return null;
        }
    }
}
