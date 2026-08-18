using System;

namespace WinBootSelfStarting.Models
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

    public class StartupEntry
    {
        public string Name { get; set; } = "";
        public string Command { get; set; } = "";
        public StartupLocation Location { get; set; }
        public bool IsEnabled => Location == StartupLocation.Registry
                                 || Location == StartupLocation.StartupFolder
                                 || Location == StartupLocation.Service
                                 || Location == StartupLocation.ScheduledTask;
        // For registry entries this is the registry value name; for files it's the file name
        public string Id { get; set; } = "";

        // Additional properties for services and scheduled tasks
        public string? ServiceStatus { get; set; }
        public string? StartType { get; set; }
    }
}
