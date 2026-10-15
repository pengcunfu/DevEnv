namespace DevEnv.Models
{
    public class ProcessStatusInfo
    {
        public ProcessState Status { get; set; }
        public string DisplayText { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int? ProcessId { get; set; }
    }

    public enum ProcessState
    {
        Unknown,
        NotConfigured,
        Stopped,
        Starting,
        Running,
        Stopping,
        ExternalRunning
    }

    public class ProcessStatusUpdatedEventArgs : EventArgs
    {
        public string ProcessName { get; }
        public ProcessStatusInfo Status { get; }

        public ProcessStatusUpdatedEventArgs(string processName, ProcessStatusInfo status)
        {
            ProcessName = processName;
            Status = status;
        }
    }
}
