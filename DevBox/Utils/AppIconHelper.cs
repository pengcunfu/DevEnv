using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Platform;

namespace ProgramBox.Utils
{
    /// <summary>
    /// 加载程序图标（Resources/icon.png / icon.ico），供窗口与托盘使用。
    /// </summary>
    public static class AppIconHelper
    {
        private static WindowIcon? _windowIcon;
        private static WindowIcon? _trayIcon;

        public static WindowIcon? Load() => _windowIcon ??= LoadPngIcon(32) ?? LoadFallbackIcon(32);

        public static WindowIcon? LoadForTray() => _trayIcon ??= LoadPngIcon(16) ?? LoadFallbackIcon(16);

        private static WindowIcon? LoadPngIcon(int size)
        {
            var bytes = ReadAssetBytes("icon.png");
            if (bytes == null)
                return null;

            try
            {
                using var input = new MemoryStream(bytes);
                using var source = new Bitmap(input);
                using var resized = new Bitmap(size, size);
                using (var g = Graphics.FromImage(resized))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(source, 0, 0, size, size);
                }

                using var output = new MemoryStream();
                resized.Save(output, ImageFormat.Png);
                output.Position = 0;
                return new WindowIcon(output);
            }
            catch
            {
                try
                {
                    using var stream = new MemoryStream(bytes);
                    return new WindowIcon(stream);
                }
                catch
                {
                    return null;
                }
            }
        }

        private static WindowIcon? LoadFallbackIcon(int size)
        {
            try
            {
                using var bitmap = new Bitmap(size, size);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.FromArgb(103, 58, 183));
                    using var brush = new SolidBrush(Color.White);
                    using var font = new Font("Segoe UI", Math.Max(8, size / 2), FontStyle.Bold, GraphicsUnit.Pixel);
                    g.DrawString("D", font, brush, size * 0.22f, size * 0.15f);
                }

                using var output = new MemoryStream();
                bitmap.Save(output, ImageFormat.Png);
                output.Position = 0;
                return new WindowIcon(output);
            }
            catch
            {
                return null;
            }
        }

        private static byte[]? ReadAssetBytes(string fileName)
        {
            foreach (var uri in GetAssetUris(fileName))
            {
                try
                {
                    using var stream = AssetLoader.Open(uri);
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
                catch
                {
                    // try next uri
                }
            }

            foreach (var path in GetFilePaths(fileName))
            {
                if (!File.Exists(path))
                    continue;

                try
                {
                    return File.ReadAllBytes(path);
                }
                catch
                {
                    // try next path
                }
            }

            return null;
        }

        private static IEnumerable<Uri> GetAssetUris(string fileName)
        {
            var assemblyName = Assembly.GetExecutingAssembly().GetName().Name ?? "DevBox";
            yield return new Uri($"avares://{assemblyName}/Resources/{fileName}");
            yield return new Uri($"avares://DevBox/Resources/{fileName}");
            yield return new Uri($"avares://ProgramBox/Resources/{fileName}");
        }

        private static IEnumerable<string> GetFilePaths(string fileName)
        {
            yield return Path.Combine(AppContext.BaseDirectory, "Resources", fileName);
            yield return Path.Combine(AppHelper.GetFullPath("Resources"), fileName);
        }
    }
}
