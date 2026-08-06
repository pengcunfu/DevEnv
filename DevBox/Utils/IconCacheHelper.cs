using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ProgramBox.Utils
{
    public static class IconCacheHelper
    {
        public static string IconsDirectory => AppHelper.GetFullPath("icons");

        public static string ComputeHash(string key)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key.ToLowerInvariant()));
            return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
        }

        public static string? SaveIconToCache(Icon icon, string cacheKey)
        {
            try
            {
                Directory.CreateDirectory(IconsDirectory);
                var destPath = Path.Combine(IconsDirectory, $"{cacheKey}.png");
                if (!File.Exists(destPath))
                {
                    using var bitmap = icon.ToBitmap();
                    bitmap.Save(destPath, ImageFormat.Png);
                }

                return destPath;
            }
            catch
            {
                return null;
            }
        }

        public static string? CopyImageToCache(string sourceImagePath, string cacheKey)
        {
            try
            {
                Directory.CreateDirectory(IconsDirectory);
                var ext = Path.GetExtension(sourceImagePath);
                if (string.IsNullOrEmpty(ext))
                    ext = ".png";

                var destPath = Path.Combine(IconsDirectory, $"{cacheKey}{ext}");
                File.Copy(sourceImagePath, destPath, overwrite: true);
                return destPath;
            }
            catch
            {
                return null;
            }
        }

        public static void TryDeleteCachedIcon(string? iconPath)
        {
            if (string.IsNullOrWhiteSpace(iconPath))
                return;

            try
            {
                var full = Path.GetFullPath(iconPath);
                var iconsRoot = Path.GetFullPath(IconsDirectory);
                if (full.StartsWith(iconsRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(full))
                    File.Delete(full);
            }
            catch
            {
                // ignore
            }
        }

        public static bool IsSamePath(string pathA, string pathB)
        {
            try
            {
                return string.Equals(
                    Path.GetFullPath(pathA),
                    Path.GetFullPath(pathB),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
