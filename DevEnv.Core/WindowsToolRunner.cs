using System.Diagnostics;

namespace DevEnv.Core;

/// <summary>启动 Windows 系统工具。</summary>
public static class WindowsToolRunner
{
    public static readonly (string Key, string Name, string Category, string Description)[] Catalog =
    [
        ("json_formatter", "JSON 格式化器", "实用工具", "格式化或压缩 JSON 文本"),
        ("hosts_editor", "Hosts 文件编辑器", "实用工具", "编辑系统 Hosts 文件（需管理员）"),
        ("image_converter", "图像格式转换器", "实用工具", "批量转换图片格式"),
        ("hash_calculator", "文件哈希计算器", "实用工具", "计算文件或文本哈希"),
        ("computer", "计算机管理", "系统管理", "综合系统管理工具"),
        ("server", "服务", "系统管理", "在内置服务监控页管理 Windows 系统服务"),
        ("config", "系统配置", "系统管理", "配置系统启动和服务"),
        ("startup", "启动项管理", "系统管理", "管理开机启动项、服务与计划任务"),
        ("regedit", "注册表", "系统管理", "编辑 Windows 注册表"),
        ("env", "环境变量", "系统管理", "管理系统环境变量"),
        ("device", "设备管理器", "硬件与设备", "管理硬件设备和驱动"),
        ("disk", "磁盘管理", "硬件与设备", "管理磁盘分区和存储"),
        ("sound", "声音设置", "硬件与设备", "调整系统声音输出设备"),
        ("display", "显示设置", "硬件与设备", "调整屏幕分辨率与显示参数"),
        ("power", "电源选项", "硬件与设备", "配置系统电源与休眠策略"),
        ("firewall", "防火墙", "网络与安全", "配置 Windows 防火墙"),
        ("security", "本地安全策略", "网络与安全", "配置安全策略"),
        ("network", "网络连接", "网络与安全", "查看和管理网络适配器"),
        ("uac", "用户账户控制", "网络与安全", "调整用户账户控制设置"),
        ("task", "任务管理器", "性能与监控", "查看和管理系统进程"),
        ("res", "资源监视器", "性能与监控", "监控系统资源使用"),
        ("event", "事件查看器", "性能与监控", "查看系统事件日志"),
        ("info", "系统信息", "性能与监控", "查看系统详细信息"),
        ("planned_task", "计划任务", "性能与监控", "管理系统定时任务"),
        // SigXor 模块并入后新增的文件与截图类工具
        ("file_preview", "文件快速预览", "文件与截图", "预览文档、图片、代码、压缩包等文件内容"),
        ("sigxor_browser", "文件浏览器", "文件与截图", "浏览目录并在内嵌面板中预览文件"),
        ("screenshot", "区域截图", "文件与截图", "截取屏幕区域，可提取文字或取色"),
        ("sigxor_settings", "语音与截屏设置", "文件与截图", "配置语音输入、OCR 模型与截图行为"),
    ];

    public static bool TryRun(string key, out string? error)
    {
        error = null;
        try
        {
            switch (key.ToLowerInvariant())
            {
                case "disk": Execute("diskmgmt.msc"); break;
                case "device": Execute("devmgmt.msc"); break;
                case "task": Execute("taskmgr"); break;
                case "planned_task": Execute("taskschd.msc"); break;
                case "regedit": Execute("regedit"); break;
                case "env": Execute("rundll32.exe", "sysdm.cpl,EditEnvironmentVariables"); break;
                case "res": Execute("resmon"); break;
                case "config": Execute("msconfig"); break;
                case "info": Execute("msinfo32"); break;
                case "event": Execute("eventvwr.msc"); break;
                case "firewall": Execute("wf.msc"); break;
                case "server":
                    HostCallbacks.OnActivateTab?.Invoke("services");
                    break;
                case "computer": Execute("compmgmt.msc"); break;
                case "security": Execute("secpol.msc"); break;
                case "control": Execute("control"); break;
                case "network": Execute("ncpa.cpl"); break;
                case "power": Execute("powercfg.cpl"); break;
                case "display": Execute("desk.cpl"); break;
                case "sound": Execute("mmsys.cpl"); break;
                case "uac": Execute("UserAccountControlSettings"); break;
                case "json_formatter":
                    HostCallbacks.OnActivateTab?.Invoke("tools/json");
                    break;
                case "hosts_editor":
                    HostCallbacks.OnActivateTab?.Invoke("tools/hosts");
                    break;
                case "image_converter":
                    HostCallbacks.OnActivateTab?.Invoke("tools/image");
                    break;
                case "hash_calculator":
                    HostCallbacks.OnActivateTab?.Invoke("tools/hash");
                    break;
                case "startup":
                    HostCallbacks.OnActivateTab?.Invoke("tools/startup");
                    break;
                // SigXor 模块：走 HostCallbacks 回到宿主，由宿主调模块能力
                case "file_preview":
                    HostCallbacks.OnOpenFilePreview?.Invoke();
                    break;
                case "sigxor_browser":
                    HostCallbacks.OnOpenSigXorBrowser?.Invoke();
                    break;
                case "screenshot":
                    HostCallbacks.OnOpenScreenshot?.Invoke();
                    break;
                case "sigxor_settings":
                    HostCallbacks.OnOpenSigXorSettings?.Invoke(SettingsSection.Voice);
                    break;
                default:
                    error = $"未知工具: {key}";
                    return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void Execute(string fileName, string? arguments = null)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments ?? string.Empty,
            UseShellExecute = true,
        });
    }
}
