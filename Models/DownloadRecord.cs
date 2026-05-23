namespace DevEnv.Models
{
    public enum DownloadStatus
    {
        Pending,
        Downloading,
        Completed,
        Failed,
        Cancelled
    }

    public class DownloadRecord
    {
        public string Id { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string SavePath { get; set; } = string.Empty;
        public string SoftwareName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DownloadStatus Status { get; set; } = DownloadStatus.Pending;
        public int Progress { get; set; }
        public long TotalSize { get; set; }
        public long DownloadedSize { get; set; }
        public double Speed { get; set; }
        public int Eta { get; set; }
        public DateTime StartTime { get; set; } = DateTime.Now;
        public DateTime? EndTime { get; set; }
        public string? ErrorMessage { get; set; }

        public string StatusText => Status switch
        {
            DownloadStatus.Pending => "等待中",
            DownloadStatus.Downloading => "下载中",
            DownloadStatus.Completed => "已完成",
            DownloadStatus.Failed => "失败",
            DownloadStatus.Cancelled => "已取消",
            _ => "未知"
        };

        public string SpeedText => Speed > 0 ? FormatBytes((long)Speed) + "/s" : "-";
        public string SizeText => TotalSize > 0 ? $"{FormatBytes(DownloadedSize)} / {FormatBytes(TotalSize)}" : FormatBytes(DownloadedSize);

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
