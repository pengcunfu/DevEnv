using System;
using System.Diagnostics;

namespace ProgramBox.Utils
{
    /// <summary>
    /// Windows系统工具帮助类
    /// </summary>
    public static class WindowsToolsHelper
    {
        /// <summary>
        /// 打开磁盘管理
        /// </summary>
        public static void OpenDiskManagement()
        {
            ExecuteCommand("diskmgmt.msc");
        }

        /// <summary>
        /// 打开设备管理器
        /// </summary>
        public static void OpenDeviceManager()
        {
            ExecuteCommand("devmgmt.msc");
        }

        /// <summary>
        /// 打开任务管理器
        /// </summary>
        public static void OpenTaskManager()
        {
            ExecuteCommand("taskmgr");
        }

        /// <summary>
        /// 打开计划任务
        /// </summary>
        public static void OpenTaskScheduler()
        {
            ExecuteCommand("taskschd.msc");
        }

        /// <summary>
        /// 打开注册表编辑器
        /// </summary>
        public static void OpenRegistryEditor()
        {
            ExecuteCommand("regedit");
        }

        /// <summary>
        /// 打开环境变量设置
        /// </summary>
        public static void OpenEnvironmentVariables()
        {
            ExecuteCommand("rundll32.exe", "sysdm.cpl,EditEnvironmentVariables");
        }

        /// <summary>
        /// 打开资源监视器
        /// </summary>
        public static void OpenResourceMonitor()
        {
            ExecuteCommand("resmon");
        }

        /// <summary>
        /// 打开系统配置
        /// </summary>
        public static void OpenSystemConfiguration()
        {
            ExecuteCommand("msconfig");
        }

        /// <summary>
        /// 打开系统信息
        /// </summary>
        public static void OpenSystemInformation()
        {
            ExecuteCommand("msinfo32");
        }

        /// <summary>
        /// 打开事件查看器
        /// </summary>
        public static void OpenEventViewer()
        {
            ExecuteCommand("eventvwr.msc");
        }

        /// <summary>
        /// 打开Windows防火墙
        /// </summary>
        public static void OpenFirewall()
        {
            ExecuteCommand("wf.msc");
        }

        /// <summary>
        /// 打开服务管理
        /// </summary>
        public static void OpenServices()
        {
            ExecuteCommand("services.msc");
        }

        /// <summary>
        /// 打开计算机管理
        /// </summary>
        public static void OpenComputerManagement()
        {
            ExecuteCommand("compmgmt.msc");
        }

        /// <summary>
        /// 打开本地安全策略
        /// </summary>
        public static void OpenLocalSecurityPolicy()
        {
            ExecuteCommand("secpol.msc");
        }

        /// <summary>
        /// 打开控制面板
        /// </summary>
        public static void OpenControlPanel()
        {
            ExecuteCommand("control");
        }

        /// <summary>
        /// 打开网络连接
        /// </summary>
        public static void OpenNetworkConnections()
        {
            ExecuteCommand("ncpa.cpl");
        }

        /// <summary>
        /// 打开程序和功能
        /// </summary>
        public static void OpenProgramsAndFeatures()
        {
            ExecuteCommand("appwiz.cpl");
        }

        /// <summary>
        /// 打开电源选项
        /// </summary>
        public static void OpenPowerOptions()
        {
            ExecuteCommand("powercfg.cpl");
        }

        /// <summary>
        /// 打开显示设置
        /// </summary>
        public static void OpenDisplaySettings()
        {
            ExecuteCommand("desk.cpl");
        }

        /// <summary>
        /// 打开声音设置
        /// </summary>
        public static void OpenSoundSettings()
        {
            ExecuteCommand("mmsys.cpl");
        }

        /// <summary>
        /// 打开用户账户控制设置
        /// </summary>
        public static void OpenUserAccountControl()
        {
            ExecuteCommand("UserAccountControlSettings");
        }

        /// <summary>
        /// 打开Windows更新
        /// </summary>
        public static void OpenWindowsUpdate()
        {
            ExecuteCommand("ms-settings:windowsupdate");
        }

        /// <summary>
        /// 打开系统设置
        /// </summary>
        public static void OpenSystemSettings()
        {
            ExecuteCommand("ms-settings:system");
        }

        /// <summary>
        /// 根据工具键值执行对应的操作
        /// </summary>
        /// <param name="toolKey">工具键值</param>
        public static void ExecuteWindowsTool(string toolKey)
        {
            switch (toolKey.ToLower())
            {
                case "disk":
                    OpenDiskManagement();
                    break;
                case "device":
                    OpenDeviceManager();
                    break;
                case "task":
                    OpenTaskManager();
                    break;
                case "planned_task":
                    OpenTaskScheduler();
                    break;
                case "regedit":
                    OpenRegistryEditor();
                    break;
                case "env":
                    OpenEnvironmentVariables();
                    break;
                case "res":
                    OpenResourceMonitor();
                    break;
                case "config":
                    OpenSystemConfiguration();
                    break;
                case "info":
                    OpenSystemInformation();
                    break;
                case "event":
                    OpenEventViewer();
                    break;
                case "firewall":
                    OpenFirewall();
                    break;
                case "server":
                    OpenServices();
                    break;
                case "computer":
                    OpenComputerManagement();
                    break;
                case "security":
                    OpenLocalSecurityPolicy();
                    break;
                case "control":
                    OpenControlPanel();
                    break;
                case "network":
                    OpenNetworkConnections();
                    break;
                case "programs":
                    OpenProgramsAndFeatures();
                    break;
                case "power":
                    OpenPowerOptions();
                    break;
                case "display":
                    OpenDisplaySettings();
                    break;
                case "sound":
                    OpenSoundSettings();
                    break;
                case "uac":
                    OpenUserAccountControl();
                    break;
                case "update":
                    OpenWindowsUpdate();
                    break;
                case "settings":
                    OpenSystemSettings();
                    break;
                default:
                    _ = AppHelper.ShowWarningAsync($"未知的工具类型: {toolKey}");
                    break;
            }
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        /// <param name="command">命令</param>
        /// <param name="arguments">参数</param>
        private static void ExecuteCommand(string command, string? arguments = null)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments ?? string.Empty,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                _ = AppHelper.ShowErrorAsync($"执行命令失败 ({command}): {ex.Message}");
            }
        }
    }
}
