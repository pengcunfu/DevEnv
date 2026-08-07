using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace ProgramBox.Converters
{
    public class PathToBitmapConverter : IValueConverter
    {
        public static readonly PathToBitmapConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string path || string.IsNullOrWhiteSpace(path))
                return null;

            // 物理文件（本地应用、文件夹、缓存的图标等）
            if (File.Exists(path))
                return LoadFile(path);

            // 程序集内嵌资源（内置工具图标），如 /Assets/Icons/win/xxx.png
            return LoadAsset(path);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();

        private static Bitmap? LoadFile(string path)
        {
            try
            {
                return new Bitmap(path);
            }
            catch
            {
                return null;
            }
        }

        private static Bitmap? LoadAsset(string path)
        {
            foreach (var uri in GetAssetUris(path))
            {
                try
                {
                    using var stream = AssetLoader.Open(uri);
                    return new Bitmap(stream);
                }
                catch
                {
                    // try next candidate
                }
            }

            return null;
        }

        private static IEnumerable<Uri> GetAssetUris(string path)
        {
            if (path.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
            {
                yield return new Uri(path);
                yield break;
            }

            if (!path.StartsWith('/'))
                yield break;

            var assemblyName = Assembly.GetExecutingAssembly().GetName().Name ?? "DevBox";
            yield return new Uri($"avares://{assemblyName}{path}");
            yield return new Uri($"avares://DevBox{path}");
            yield return new Uri($"avares://ProgramBox{path}");
        }
    }
}
