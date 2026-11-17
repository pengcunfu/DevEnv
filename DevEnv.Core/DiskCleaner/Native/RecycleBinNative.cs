using System.Runtime.InteropServices;

namespace DevEnv.Core.DiskCleaner.Native;

/// <summary>回收站查询与清空的 Shell32 封装（不弹系统确认框与进度条）。</summary>
internal static class RecycleBinNative
{
    public const uint SherbNoConfirmation = 0x00000001;
    public const uint SherbNoProgressUi = 0x00000002;
    public const uint SherbNoSound = 0x00000004;

    // 与 shellapi.h 的自然对齐保持一致：x64 下 cbSize 为 24，
    // 写错 cbSize 会让 SHQueryRecycleBin 直接失败。
    [StructLayout(LayoutKind.Sequential)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    public static (bool Ok, long SizeBytes, long ItemCount) Query(string rootPath)
    {
        var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
        var hr = SHQueryRecycleBin(rootPath, ref info);
        return hr == 0
            ? (true, Math.Max(0, info.i64Size), Math.Max(0, info.i64NumItems))
            : (false, 0, 0);
    }

    public static int Empty(string rootPath)
    {
        return SHEmptyRecycleBin(
            IntPtr.Zero,
            rootPath,
            SherbNoConfirmation | SherbNoProgressUi | SherbNoSound);
    }
}
