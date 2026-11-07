using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DevEnv.Host.SigXor.Services;

/// <summary>
/// 读取桌面当前选中的文件 / 文件夹。
///
/// 桌面不在 Shell.Application.Windows() 里，所以不能用 <see cref="ExplorerSelectionService"/> 的方式；
/// 这里直接找到桌面的 SHELLDLL_DefView 子窗口 SysListView32，用 LVM_GETNEXTITEM 取选中项索引，
/// 再用 LVM_GETITEMTEXTW 读取显示名。ListView 属于 explorer.exe，读写 LVITEM 需要跨进程内存。
/// 显示名可能省略已知扩展名（如 .lnk），因此再按「去掉扩展名后同名」在桌面目录里回查。
/// </summary>
public sealed class DesktopSelectionService
{
    private const uint LvmGetSelectedCount = 0x1032;
    private const uint LvmGetNextItem = 0x100C;
    private const uint LvmGetItemTextW = 0x1073;
    private const uint LvniSelected = 0x0002;
    private const uint LvifText = 0x0001;
    private const int TextChars = 1024;

    private const uint ProcessVmOperation = 0x0008;
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessVmWrite = 0x0020;
    private const uint MemCommit = 0x1000;
    private const uint MemReserve = 0x2000;
    private const uint MemRelease = 0x8000;
    private const uint PageReadWrite = 0x04;

    /// <summary>返回桌面选中项的完整路径；没有选中或读取失败时返回空列表。</summary>
    public List<string> GetSelectedFiles()
    {
        var result = new List<string>();
        IntPtr listView = FindDesktopListView();
        if (listView == IntPtr.Zero) return result;

        var names = ReadSelectedNames(listView);
        if (names.Count == 0) return result;

        string[] roots =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
        ];

        foreach (string name in names)
        {
            foreach (string root in roots)
            {
                if (string.IsNullOrWhiteSpace(root)) continue;
                string? path = Resolve(root, name);
                if (path is not null)
                {
                    result.Add(path);
                    break;
                }
            }
        }
        return result;
    }

    /// <summary>桌面目录里的真实文件名（显示名可能省略扩展名）。</summary>
    private static string? Resolve(string root, string displayName)
    {
        try
        {
            string direct = Path.Combine(root, displayName);
            if (File.Exists(direct) || Directory.Exists(direct)) return direct;

            foreach (string file in Directory.EnumerateFiles(root))
            {
                if (string.Equals(Path.GetFileNameWithoutExtension(file), displayName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return file;
                }
            }

            foreach (string dir in Directory.EnumerateDirectories(root))
            {
                if (string.Equals(Path.GetFileName(dir), displayName, StringComparison.OrdinalIgnoreCase))
                    return dir;
            }
        }
        catch
        {
            // 目录不可访问时按未匹配处理
        }
        return null;
    }

    private static IntPtr FindDesktopListView()
    {
        IntPtr progman = FindWindow("Progman", null);
        IntPtr defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (defView == IntPtr.Zero)
        {
            // Windows 10/11 的桌面有时挂在某个 WorkerW 下而不是 Progman
            EnumWindows((hwnd, _) =>
            {
                var className = new StringBuilder(64);
                GetClassName(hwnd, className, className.Capacity);
                if (className.ToString() != "WorkerW") return true;

                IntPtr child = FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (child == IntPtr.Zero) return true;
                defView = child;
                return false;
            }, IntPtr.Zero);
        }

        return defView == IntPtr.Zero
            ? IntPtr.Zero
            : FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
    }

    private static List<string> ReadSelectedNames(IntPtr listView)
    {
        var names = new List<string>();
        try
        {
            int selected = (int)SendMessage(listView, LvmGetSelectedCount, IntPtr.Zero, IntPtr.Zero);
            if (selected <= 0) return names;

            GetWindowThreadProcessId(listView, out uint pid);
            IntPtr process = OpenProcess(ProcessVmOperation | ProcessVmRead | ProcessVmWrite, false, pid);
            if (process == IntPtr.Zero) return names;

            int itemSize = Marshal.SizeOf<LvItem>();
            IntPtr remote = VirtualAllocEx(process, IntPtr.Zero,
                (nuint)(itemSize + TextChars * sizeof(char)), MemCommit | MemReserve, PageReadWrite);
            if (remote == IntPtr.Zero)
            {
                CloseHandle(process);
                return names;
            }

            try
            {
                IntPtr remoteText = remote + itemSize;
                int index = -1;
                while ((index = (int)SendMessage(listView, LvmGetNextItem, (IntPtr)index, (IntPtr)LvniSelected)) >= 0)
                {
                    var item = new LvItem
                    {
                        Mask = LvifText,
                        SubItem = 0,
                        Text = remoteText,
                        TextCapacity = TextChars,
                    };
                    WriteStruct(process, remote, item);
                    SendMessage(listView, LvmGetItemTextW, (IntPtr)index, remote);

                    string? name = ReadString(process, remoteText, TextChars);
                    if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
                }
            }
            finally
            {
                VirtualFreeEx(process, remote, 0, MemRelease);
                CloseHandle(process);
            }
        }
        catch
        {
            // 桌面 ListView 结构变化或权限不足时按无选中处理
        }
        return names;
    }

    private static void WriteStruct(IntPtr process, IntPtr remote, LvItem item)
    {
        int size = Marshal.SizeOf<LvItem>();
        IntPtr local = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(item, local, false);
            var buffer = new byte[size];
            Marshal.Copy(local, buffer, 0, size);
            WriteProcessMemory(process, remote, buffer, (nuint)size, out _);
        }
        finally
        {
            Marshal.FreeHGlobal(local);
        }
    }

    private static string? ReadString(IntPtr process, IntPtr remoteText, int chars)
    {
        var buffer = new byte[chars * sizeof(char)];
        if (!ReadProcessMemory(process, remoteText, buffer, (nuint)buffer.Length, out _)) return null;

        string text = Encoding.Unicode.GetString(buffer);
        int nul = text.IndexOf('\0');
        return nul >= 0 ? text[..nul] : text;
    }

    #region Win32

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string? windowName);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern IntPtr VirtualAllocEx(IntPtr process, IntPtr address, nuint size, uint type, uint protect);

    [DllImport("kernel32.dll")]
    private static extern bool VirtualFreeEx(IntPtr process, IntPtr address, nuint size, uint freeType);

    [DllImport("kernel32.dll")]
    private static extern bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] buffer, nuint size,
        out nuint written);

    [DllImport("kernel32.dll")]
    private static extern bool ReadProcessMemory(IntPtr process, IntPtr address, byte[] buffer, nuint size,
        out nuint read);

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    /// <summary>与目标 ListView 同进程位数匹配的 LVITEM（指针字段用 IntPtr）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct LvItem
    {
        public uint Mask;
        public int Item;
        public int SubItem;
        public uint State;
        public uint StateMask;
        public IntPtr Text;
        public int TextCapacity;
        public int Image;
        public IntPtr Param;
        public int Indent;
        public int GroupId;
        public uint Columns;
        public IntPtr ColumnFormats;
        public IntPtr ColumnFormat;
        public int Group;
    }

    #endregion
}
