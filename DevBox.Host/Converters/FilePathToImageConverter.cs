using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace DevBox.Host.Converters;

/// <summary>
/// 本地图片路径 → ImageSource。图标缓存目录下的 PNG 由 <c>IconHelper</c> 落盘，
/// 这里只负责加载并冻结，避免锁住文件（否则图标刷新/删除会失败）。
/// 同一路径只解码一次。
/// </summary>
public sealed class FilePathToImageConverter : IValueConverter
{
    private static readonly Dictionary<string, BitmapSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var path = value as string;
        if (string.IsNullOrWhiteSpace(path))
            return null;

        lock (Cache)
        {
            if (Cache.TryGetValue(path, out var cached))
                return cached;

            BitmapSource? image = null;
            try
            {
                if (File.Exists(path))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad; // 解码进内存，不持有文件句柄
                    bitmap.UriSource = new Uri(path);
                    bitmap.EndInit();
                    bitmap.Freeze();
                    image = bitmap;
                }
            }
            catch
            {
                image = null;
            }

            Cache[path] = image;
            return image;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;

    /// <summary>图标刷新后调用，下一次绑定重新解码。</summary>
    public static void Invalidate(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        lock (Cache)
        {
            Cache.Remove(path);
        }
    }

    public static void Clear()
    {
        lock (Cache)
        {
            Cache.Clear();
        }
    }
}