namespace DevEnv.Models
{
    public class ServiceStatusInfo
    {
        public ServiceState Status { get; set; }
        public string DisplayText { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
    }

    public enum ServiceState
    {
        Unknown,
        NotFound,
        Stopped,
        Starting,
        Running,
        Stopping,
        Paused
    }

    public class ServiceStatusUpdatedEventArgs : EventArgs
    {
        public string ServiceName { get; }
        public ServiceStatusInfo Status { get; }

        public ServiceStatusUpdatedEventArgs(string serviceName, ServiceStatusInfo status)
        {
            ServiceName = serviceName;
            Status = status;
        }
    }
}