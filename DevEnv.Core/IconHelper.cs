using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace DevEnv.Core;

/// <summary>图标缓存：文档\FNSoftware\DevEnv\icons。</summary>
public static class IconHelper
{
    /// <summary>启动器里的图标要显示到 56px 以上，低于这个宽度的缓存视为旧图需要重提。</summary>
    private const int LauncherIconSize = 64;

    /// <summary>图标缓存文件写入锁：开始菜单后台加载与 UI 刷新可能同时写同一目录。</summary>
    private static readonly object IconGate = new();

    public static string IconsDirectory => Path.Combine(AppPaths.UserDataDirectory, "icons");

    /// <summary>exe 图标缓存文件路径（不一定存在）。</summary>
    public static string GetExecutableIconPath(string exePath) =>
        Path.Combine(IconsDirectory, $"exe_{ComputeHash(exePath)}.png");

    public static string ComputeHash(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key.ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    /// <summary>
    /// 提取 exe 图标并写回缓存。优先取 256px 大图标（全屏启动器需要），
    /// 取不到再退回系统默认的 32px；每次调用都会覆盖，刷新图标才真正生效。
    /// </summary>
    public static string? ExtractExecutableIcon(string exePath)
    {
        var destPath = GetExecutableIconPath(exePath);
        try
        {
            using var bitmap = ExtractLargeBitmap(exePath);
            if (bitmap is null)
                return File.Exists(destPath) ? destPath : null;

            lock (IconGate)
            {
                Directory.CreateDirectory(IconsDirectory);
                bitmap.Save(destPath, ImageFormat.Png);
            }
            return destPath;
        }
        catch
        {
            return File.Exists(destPath) ? destPath : null;
        }
    }

    /// <summary>
    /// 开始菜单批量加载用：缓存已经够大就直接复用，不做重复提取。
    /// 与 <see cref="ExtractExecutableIcon"/> 的区别是不强制刷新（刷新图标仍走原方法）。
    /// </summary>
    public static string? EnsureCachedExecutableIcon(string exePath, int minSize = LauncherIconSize)
    {
        var cached = GetExecutableIconPath(exePath);
        try
        {
            if (File.Exists(cached) && GetPixelWidth(cached) >= minSize)
                return cached;
        }
        catch
        {
            // 缓存文件损坏时按未命中处理
        }

        return ExtractExecutableIcon(exePath);
    }

    /// <summary>
    /// 为 .lnk / .url 等非 PE 文件提取 shell 关联图标并缓存。缓存键包含解析目标，
    /// 快捷方式改目标后不会继续用旧图。
    /// </summary>
    public static string? EnsureAssociatedIcon(string filePath, string cacheKey, bool force = false)
    {
        var destPath = Path.Combine(IconsDirectory, $"lnk_{ComputeHash(cacheKey)}.png");
        try
        {
            if (!force && File.Exists(destPath))
                return destPath;

            using var icon = Icon.ExtractAssociatedIcon(filePath);
            if (icon is null)
                return File.Exists(destPath) ? destPath : null;

            lock (IconGate)
            {
                Directory.CreateDirectory(IconsDirectory);
                using var bitmap = icon.ToBitmap();
                bitmap.Save(destPath, ImageFormat.Png);
            }
            return destPath;
        }
        catch
        {
            return File.Exists(destPath) ? destPath : null;
        }
    }

    /// <summary>
    /// 旧版本按 32px 缓存图标，放到启动器里会发虚。这里检查现有缓存宽度，
    /// 不够大就重新提取；返回可用的图标路径。
    /// </summary>
    public static string? EnsureLargeIcon(string exePath, string? iconPath, int minSize = LauncherIconSize)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(iconPath)
                && File.Exists(iconPath)
                && GetPixelWidth(iconPath) >= minSize)
            {
                return iconPath;
            }
            return ExtractExecutableIcon(exePath) ?? iconPath;
        }
        catch
        {
            return iconPath;
        }
    }

    private static int GetPixelWidth(string path)
    {
        using var image = Image.FromFile(path);
        return image.Width;
    }

    /// <summary>按从大到小的尺寸尝试提取，取第一个成功的；都失败时退回 32px 关联图标。</summary>
    private static Bitmap? ExtractLargeBitmap(string exePath)
    {
        foreach (var size in new[] { 256, 128, 96, 64, 48 })
        {
            var handles = new IntPtr[1];
            var ids = new uint[1];
            var count = PrivateExtractIcons(exePath, 0, size, size, handles, ids, 1, 0);
            if (count == 0 || handles[0] == IntPtr.Zero)
                continue;

            try
            {
                using var icon = Icon.FromHandle(handles[0]);
                return icon.ToBitmap();
            }
            finally
            {
                DestroyIcon(handles[0]);
            }
        }

        using var fallback = Icon.ExtractAssociatedIcon(exePath);
        return fallback?.ToBitmap();
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint PrivateExtractIcons(
        string szFileName,
        int nIconIndex,
        int cxIcon,
        int cyIcon,
        IntPtr[] phicon,
        uint[] piconid,
        uint nIcons,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

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
