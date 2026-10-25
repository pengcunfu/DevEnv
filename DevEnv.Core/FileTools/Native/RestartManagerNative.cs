using System.Runtime.InteropServices;
using System.Text;

namespace DevEnv.Core.FileTools.Native;

/// <summary>
/// Restart Manager（rstrtmgr.dll）P/Invoke。
/// 这是 Windows 官方推荐的「谁占用了这个文件」检测方式，不需要管理员权限，
/// 也不会像枚举全局句柄那样扫描整张句柄表。
/// </summary>
internal static class RestartManagerNative
{
    internal const int ErrorSuccess = 0;
    internal const int ErrorMoreData = 234;
    internal const int CchRmSessionKey = 32;
    internal const int CchRmMaxAppName = 255;
    internal const int CchRmMaxSvcName = 63;

    internal enum RmAppType
    {
        RmUnknownApp = 0,
        RmMainWindow = 1,
        RmOtherWindow = 2,
        RmService = 3,
        RmExplorer = 4,
        RmConsole = 5,
        RmCritical = 1000,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RmUniqueProcess
    {
        public int ProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct RmProcessInfo
    {
        public RmUniqueProcess Process;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxAppName + 1)]
        public string AppName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxSvcName + 1)]
        public string ServiceShortName;

        public RmAppType ApplicationType;
        public uint AppStatus;
        public uint TsSessionId;

        [MarshalAs(UnmanagedType.Bool)]
        public bool Restartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    internal static extern int RmStartSession(out uint sessionHandle, int sessionFlags, StringBuilder sessionKey);

    [DllImport("rstrtmgr.dll")]
    internal static extern int RmEndSession(uint sessionHandle);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    internal static extern int RmRegisterResources(
        uint sessionHandle,
        uint fileCount,
        string[]? fileNames,
        uint applicationCount,
        RmUniqueProcess[]? applications,
        uint serviceCount,
        string[]? serviceNames);

    [DllImport("rstrtmgr.dll")]
    internal static extern int RmGetList(
        uint sessionHandle,
        out uint processInfoNeeded,
        ref uint processInfoCount,
        [In, Out] RmProcessInfo[]? affectedApps,
        out uint rebootReasons);
}
