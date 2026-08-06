using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;
using ProgramBox.Models;

namespace ProgramBox.Utils
{
    /// <summary>
    /// 本地应用：图标提取、元数据与启动
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class NativeAppHelper
    {
        public static NativeAtom? CreateFromExecutable(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath))
                return null;

            var fullPath = Path.GetFullPath(exePath.Trim().Trim('"'));
            if (!fullPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
                return null;

            var name = GetDisplayName(fullPath);
            var iconPath = ExtractExecutableIconToCache(fullPath) ?? string.Empty;
            var tag = Path.GetFileNameWithoutExtension(fullPath);

            return new NativeAtom(name, iconPath, tag, fullPath, fullPath);
        }

        public static string GetDisplayName(string exePath)
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(exePath);
                if (!string.IsNullOrWhiteSpace(info.ProductName))
                    return info.ProductName.Trim();
                if (!string.IsNullOrWhiteSpace(info.FileDescription))
                    return info.FileDescription.Trim();
            }
            catch
            {
                // ignore
            }

            return Path.GetFileNameWithoutExtension(exePath);
        }

        public static string? ExtractExecutableIconToCache(string exePath)
        {
            try
            {
                using var icon = Icon.ExtractAssociatedIcon(exePath);
                if (icon == null)
                    return null;

                var cacheKey = $"exe_{IconCacheHelper.ComputeHash(exePath)}";
                return IconCacheHelper.SaveIconToCache(icon, cacheKey);
            }
            catch
            {
                return null;
            }
        }

        public static void TryDeleteCachedIcon(string? iconPath)
            => IconCacheHelper.TryDeleteCachedIcon(iconPath);

        public static bool IsSameExecutable(string pathA, string pathB)
            => IconCacheHelper.IsSamePath(pathA, pathB);
    }
}
