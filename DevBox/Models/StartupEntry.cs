namespace ProgramBox.Models
{
    public enum StartupLocation
    {
        Registry,
        StartupFolder,
        DisabledRegistry,
        DisabledFolder,
        Service,
        ScheduledTask
    }

    /// <summary>
    /// 开机启动项 / 自启服务 / 计划任务条目
    /// </summary>
    public class StartupEntry
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Command { get; set; } = "";
        public StartupLocation Location { get; set; }
        public string? ServiceStatus { get; set; }
        public string? StartType { get; set; }

        public bool IsEnabled =>
            Location is StartupLocation.Registry
                or StartupLocation.StartupFolder
                or StartupLocation.Service
                or StartupLocation.ScheduledTask;

        public string LocationDisplay => Location switch
        {
            StartupLocation.Registry => "注册表",
            StartupLocation.StartupFolder => "启动文件夹",
            StartupLocation.DisabledRegistry => "注册表(已禁用)",
            StartupLocation.DisabledFolder => "启动文件夹(已禁用)",
            StartupLocation.Service => "服务",
            StartupLocation.ScheduledTask => "计划任务",
            _ => Location.ToString()
        };

        public string EnabledDisplay => IsEnabled ? "是" : "否";
    }
}
