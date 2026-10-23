using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;

namespace DevBox.Core;

/// <summary>图标缓存：文档\FNSoftware\DevEnv\icons。</summary>
public static class IconHelper
{
    public static string IconsDirectory => Path.Combine(AppPaths.UserDataDirectory, "icons");

    public static string ComputeHash(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key.ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    public static string? ExtractExecutableIcon(string exePath)
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon is null)
                return null;
            return SaveIcon(icon, $"exe_{ComputeHash(exePath)}");
        }
        catch
        {
            return null;
        }
    }

    public static string GetDisplayName(string exePath)
    {
        try
        {
            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
            if (!string.IsNullOrWhiteSpace(info.ProductName))
                return info.ProductName.Trim();
            if (!string.IsNullOrWhiteSpace(info.FileDescription))
                return info.FileDescription.Trim();
        }
        catch { /* ignore */ }

        return Path.GetFileNameWithoutExtension(exePath);
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

    public static string? SaveBytesToCache(byte[] bytes, string cacheKey, string ext = ".png")
    {
        try
        {
            Directory.CreateDirectory(IconsDirectory);
            var destPath = Path.Combine(IconsDirectory, $"{cacheKey}{ext}");
            File.WriteAllBytes(destPath, bytes);
            return destPath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 把历史遗留的图标绝对路径归一化到当前图标目录。
    /// data.json 里存的是目录迁移之前的旧路径，目录搬空后这些路径全部失效，
    /// 但同名文件就在当前目录里。存在则原样返回；不存在则按文件名回退；再找不到返回 null。
    /// </summary>
    public static string? ResolveIconPath(string? recorded)
    {
        if (string.IsNullOrWhiteSpace(recorded))
            return null;

        try
        {
            if (File.Exists(recorded))
                return recorded;

            var current = Path.Combine(IconsDirectory, Path.GetFileName(recorded));
            return File.Exists(current) ? current : null;
        }
        catch
        {
            return null;
        }
    }

    public static bool IsUnderIconsDir(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetFullPath(IconsDirectory);
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string? SaveIcon(Icon icon, string cacheKey)
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
}
