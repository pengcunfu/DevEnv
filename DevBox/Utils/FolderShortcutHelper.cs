using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ProgramBox.Models;

namespace ProgramBox.Utils
{
    [SupportedOSPlatform("windows")]
    public static class FolderShortcutHelper
    {
        private const uint ShgfiIcon = 0x000000100;
        private const uint ShgfiLargeicon = 0x000000000;
        private const uint FileAttributeDirectory = 0x00000010;

        public static FolderAtom? CreateFromDirectory(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                return null;

            var fullPath = Path.GetFullPath(folderPath.Trim().Trim('"'));
            if (!Directory.Exists(fullPath))
                return null;

            var defaultName = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(defaultName))
                defaultName = fullPath;

            var iconPath = ExtractFolderIconToCache(fullPath) ?? string.Empty;
            return new FolderAtom(defaultName, iconPath, "folder", fullPath, fullPath);
        }

        public static string? ExtractFolderIconToCache(string folderPath)
        {
            IntPtr hIcon = IntPtr.Zero;
            try
            {
                var shfi = new Shfileinfo();
                _ = SHGetFileInfo(
                    folderPath,
                    FileAttributeDirectory,
                    ref shfi,
                    (uint)Marshal.SizeOf<Shfileinfo>(),
                    ShgfiIcon | ShgfiLargeicon);

                if (shfi.hIcon == IntPtr.Zero)
                    return null;

                hIcon = shfi.hIcon;
                using var icon = (Icon)Icon.FromHandle(hIcon).Clone();
                var cacheKey = $"folder_{IconCacheHelper.ComputeHash(folderPath)}";
                return IconCacheHelper.SaveIconToCache(icon, cacheKey);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (hIcon != IntPtr.Zero)
                    DestroyIcon(hIcon);
            }
        }

        public static string? SetCustomIcon(FolderAtom folder, string imageFilePath)
        {
            if (!File.Exists(imageFilePath))
                return null;

            var cacheKey = $"folder_{IconCacheHelper.ComputeHash(folder.FolderPath)}_custom";
            IconCacheHelper.TryDeleteCachedIcon(folder.IconPath);
            return IconCacheHelper.CopyImageToCache(imageFilePath, cacheKey);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref Shfileinfo psfi,
            uint cbFileInfo,
            uint uFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct Shfileinfo
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }
    }
}
