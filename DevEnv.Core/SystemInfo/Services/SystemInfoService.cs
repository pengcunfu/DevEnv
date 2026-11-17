using System.Globalization;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using DevEnv.Core.ProcessWatch.Models;
using DevEnv.Core.SystemInfo.Models;
using Microsoft.Win32;

namespace DevEnv.Core.SystemInfo.Services;

/// <summary>
/// 系统信息快照：操作系统、计算机、处理器、内存、磁盘、显卡与网络适配器。
/// 只读采集，WMI 查询失败时降级为注册表 / 环境信息，不会中断整份报告。
/// </summary>
public sealed class SystemInfoService
{
    public IReadOnlyList<SystemInfoGroup> Collect()
    {
        var groups = new List<SystemInfoGroup>
        {
            CollectOperatingSystem(),
            CollectComputer(),
            CollectProcessor(),
            CollectMemory(),
            CollectDisks(),
        };

        var gpu = CollectGpu();
        if (gpu.Items.Count > 0)
            groups.Add(gpu);

        var network = CollectNetwork();
        if (network.Items.Count > 0)
            groups.Add(network);

        return groups;
    }

    /// <summary>Windows 11 的注册表 ProductName 仍可能是「Windows 10」，按 build 号纠正显示名。</summary>
    public static string NormalizeOsName(string? productName, int build)
    {
        var name = string.IsNullOrWhiteSpace(productName) ? "Windows" : productName.Trim();
        if (build >= 22000 && name.StartsWith("Windows 10", StringComparison.OrdinalIgnoreCase))
            name = "Windows 11" + name["Windows 10".Length..];

        return name;
    }

    public static string FormatUptime(TimeSpan uptime)
    {
        if (uptime < TimeSpan.Zero)
            uptime = TimeSpan.Zero;

        var days = (int)uptime.TotalDays;
        if (days > 0)
            return $"{days} 天 {uptime.Hours} 小时 {uptime.Minutes} 分";

        if (uptime.Hours > 0)
            return $"{uptime.Hours} 小时 {uptime.Minutes} 分";

        return $"{uptime.Minutes} 分 {uptime.Seconds} 秒";
    }

    private static SystemInfoGroup CollectOperatingSystem()
    {
        var items = new List<SystemInfoItem>();
        var build = Environment.OSVersion.Version.Build;
        var version = Environment.OSVersion.Version;
        string? displayVersion = null;
        var updateBuild = 0;
        DateTime? installDate = null;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key is not null)
            {
                var product = key.GetValue("ProductName") as string;
                displayVersion = key.GetValue("DisplayVersion") as string;
                if (key.GetValue("CurrentBuildNumber") is string buildText
                    && int.TryParse(buildText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedBuild))
                {
                    build = parsedBuild;
                }

                if (key.GetValue("UBR") is int ubr)
                    updateBuild = ubr;

                if (key.GetValue("InstallDate") is int seconds)
                    installDate = DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime;

                items.Add(new SystemInfoItem("系统名称", NormalizeOsName(product, build)));
            }
        }
        catch
        {
            // 注册表读取失败时继续用 Environment 信息
        }

        if (items.Count == 0)
            items.Add(new SystemInfoItem("系统名称", NormalizeOsName(RuntimeInformation.OSDescription, build)));

        var versionText = string.IsNullOrWhiteSpace(displayVersion)
            ? $"{version.Major}.{version.Minor}.{build}"
            : displayVersion;
        if (updateBuild > 0)
            versionText += $" · Build {build}.{updateBuild}";
        else
            versionText += $" · Build {build}";

        items.Add(new SystemInfoItem("系统版本", versionText));
        items.Add(new SystemInfoItem("系统架构", RuntimeInformation.OSArchitecture.ToString()));

        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var bootTime = DateTime.Now - uptime;
        items.Add(new SystemInfoItem("运行时长", FormatUptime(uptime)));
        items.Add(new SystemInfoItem("启动时间", bootTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));

        if (installDate is { } installed)
            items.Add(new SystemInfoItem("安装日期", installed.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));

        return new SystemInfoGroup("操作系统", items);
    }

    private static SystemInfoGroup CollectComputer()
    {
        var items = new List<SystemInfoItem>
        {
            new("计算机名", Environment.MachineName),
            new("当前用户", $"{Environment.UserDomainName}\\{Environment.UserName}"),
            new(".NET 版本", RuntimeInformation.FrameworkDescription),
            new("系统区域", CultureInfo.CurrentCulture.DisplayName),
            new("界面语言", CultureInfo.CurrentUICulture.Name),
            new("时区", TimeZoneInfo.Local.DisplayName),
        };

        return new SystemInfoGroup("计算机", items);
    }

    private static SystemInfoGroup CollectProcessor()
    {
        var items = new List<SystemInfoItem>();
        var logicalCount = Environment.ProcessorCount;
        var physicalCount = 0;
        var maxClock = 0;
        string? name = null;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            name = key?.GetValue("ProcessorNameString") as string;
            if (key?.GetValue("~MHz") is int mhz)
                maxClock = mhz;
        }
        catch
        {
            // 忽略
        }

        try
        {
            foreach (var row in Query("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor"))
            {
                name ??= row.GetValueOrDefault("Name") as string;
                physicalCount = Math.Max(physicalCount, ToInt(row.GetValueOrDefault("NumberOfCores")));
                logicalCount = Math.Max(logicalCount, ToInt(row.GetValueOrDefault("NumberOfLogicalProcessors")));
                maxClock = Math.Max(maxClock, ToInt(row.GetValueOrDefault("MaxClockSpeed")));
            }
        }
        catch
        {
            // WMI 不可用时保留注册表 / Environment 结果
        }

        items.Add(new SystemInfoItem("处理器", string.IsNullOrWhiteSpace(name) ? "未知" : name.Trim()));
        items.Add(new SystemInfoItem("物理核心", physicalCount > 0 ? physicalCount.ToString(CultureInfo.InvariantCulture) : "未知"));
        items.Add(new SystemInfoItem("逻辑处理器", logicalCount.ToString(CultureInfo.InvariantCulture)));
        if (maxClock > 0)
            items.Add(new SystemInfoItem("主频", $"{maxClock} MHz"));

        return new SystemInfoGroup("处理器", items);
    }

    private static SystemInfoGroup CollectMemory()
    {
        var items = new List<SystemInfoItem>();
        var status = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (GlobalMemoryStatusEx(ref status))
        {
            var total = (long)status.ullTotalPhys;
            var available = (long)status.ullAvailPhys;
            var used = Math.Max(0, total - available);
            items.Add(new SystemInfoItem("物理内存", ByteFormatter.FormatBytes(total)));
            items.Add(new SystemInfoItem("已用内存", ByteFormatter.FormatBytes(used)));
            items.Add(new SystemInfoItem("可用内存", ByteFormatter.FormatBytes(available)));
            items.Add(new SystemInfoItem("内存占用", $"{status.dwMemoryLoad}%"));
            if (status.ullTotalPageFile > 0)
                items.Add(new SystemInfoItem("页面文件", ByteFormatter.FormatBytes((long)status.ullTotalPageFile)));
        }
        else
        {
            items.Add(new SystemInfoItem("物理内存", "读取失败"));
        }

        return new SystemInfoGroup("内存", items);
    }

    private static SystemInfoGroup CollectDisks()
    {
        var items = new List<SystemInfoItem>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady || drive.DriveType is not (DriveType.Fixed or DriveType.Removable))
                    continue;

                var total = drive.TotalSize;
                var free = drive.AvailableFreeSpace;
                var used = Math.Max(0, total - free);
                var label = drive.VolumeLabel;
                var driveName = drive.Name.TrimEnd('\\');
                var title = string.IsNullOrWhiteSpace(label) ? driveName : $"{driveName}（{label}）";
                items.Add(new SystemInfoItem(
                    title,
                    $"{drive.DriveFormat} · 已用 {ByteFormatter.FormatBytes(used)} / 共 {ByteFormatter.FormatBytes(total)} · 可用 {ByteFormatter.FormatBytes(free)}"));
            }
        }
        catch
        {
            // 忽略单个磁盘读取失败
        }

        if (items.Count == 0)
            items.Add(new SystemInfoItem("磁盘", "未找到可用磁盘"));

        return new SystemInfoGroup("磁盘", items);
    }

    private static SystemInfoGroup CollectGpu()
    {
        var items = new List<SystemInfoItem>();
        try
        {
            foreach (var row in Query("SELECT Name, DriverVersion FROM Win32_VideoController"))
            {
                var name = (row.GetValueOrDefault("Name") as string)?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var driver = (row.GetValueOrDefault("DriverVersion") as string)?.Trim();
                items.Add(new SystemInfoItem(
                    name,
                    string.IsNullOrWhiteSpace(driver) ? "驱动版本未知" : $"驱动 {driver}"));
            }
        }
        catch
        {
            // WMI 不可用时跳过显卡
        }

        return new SystemInfoGroup("显卡", items);
    }

    private static SystemInfoGroup CollectNetwork()
    {
        var items = new List<SystemInfoItem>();
        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up
                    || adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                var details = new List<string> { adapter.NetworkInterfaceType.ToString() };
                if (adapter.Speed > 0)
                    details.Add($"{adapter.Speed / 1_000_000d:0.#} Mbps");

                var addresses = adapter.GetIPProperties().UnicastAddresses
                    .Where(address => address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    .Select(address => address.Address.ToString())
                    .ToList();
                if (addresses.Count > 0)
                    details.Add(string.Join(" / ", addresses));

                items.Add(new SystemInfoItem(adapter.Name, string.Join(" · ", details)));
            }
        }
        catch
        {
            // 忽略网络适配器读取失败
        }

        return new SystemInfoGroup("网络适配器", items);
    }

    private static IEnumerable<Dictionary<string, object?>> Query(string wql)
    {
        var rows = new List<Dictionary<string, object?>>();
        try
        {
            using var searcher = new ManagementObjectSearcher(wql);
            using var collection = searcher.Get();
            foreach (ManagementObject item in collection)
            {
                using (item)
                {
                    var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var property in item.Properties)
                        row[property.Name] = property.Value;
                    rows.Add(row);
                }
            }
        }
        catch
        {
            // 由调用方按空结果降级
        }

        return rows;
    }

    private static int ToInt(object? value)
    {
        try
        {
            return value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
