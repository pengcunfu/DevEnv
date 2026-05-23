using System.IO;
using System.Text.Json;
using DevEnv.Models;

namespace DevEnv.Services
{
    public class DownloadHistoryService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        private readonly string _historyPath;
        private readonly object _lock = new();
        private List<DownloadRecord> _history = [];

        public event EventHandler? HistoryChanged;

        public DownloadHistoryService()
        {
            AppPaths.EnsureDirectories();
            _historyPath = AppPaths.DownloadHistoryFile;
            _history = LoadHistory();
        }

        public IReadOnlyList<DownloadRecord> GetAll() => _history.ToList();

        public IReadOnlyList<DownloadRecord> GetActive() =>
            _history.Where(r => r.Status is DownloadStatus.Pending or DownloadStatus.Downloading).ToList();

        public DownloadRecord? GetById(string id) => _history.FirstOrDefault(r => r.Id == id);

        public DownloadRecord Add(string url, string fileName, string savePath, string softwareName, string version)
        {
            var record = new DownloadRecord
            {
                Id = $"download_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                Url = url,
                FileName = fileName,
                SavePath = savePath,
                SoftwareName = softwareName,
                Version = version,
                Status = DownloadStatus.Pending,
                StartTime = DateTime.Now
            };

            lock (_lock)
            {
                _history.Insert(0, record);
                SaveHistory();
            }

            HistoryChanged?.Invoke(this, EventArgs.Empty);
            return record;
        }

        public void Update(string id, Action<DownloadRecord> update)
        {
            lock (_lock)
            {
                var record = _history.FirstOrDefault(r => r.Id == id);
                if (record == null) return;

                update(record);
                if (record.Status is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled)
                    record.EndTime = DateTime.Now;

                SaveHistory();
            }

            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Remove(string id)
        {
            lock (_lock)
            {
                _history.RemoveAll(r => r.Id == id);
                SaveHistory();
            }

            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearCompleted()
        {
            lock (_lock)
            {
                _history.RemoveAll(r => r.Status == DownloadStatus.Completed);
                SaveHistory();
            }

            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearFailed()
        {
            lock (_lock)
            {
                _history.RemoveAll(r => r.Status == DownloadStatus.Failed);
                SaveHistory();
            }

            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearAll()
        {
            lock (_lock)
            {
                _history.Clear();
                SaveHistory();
            }

            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public (int Total, int Completed, int Failed, int Downloading, long TotalBytes) GetStatistics()
        {
            lock (_lock)
            {
                return (
                    _history.Count,
                    _history.Count(r => r.Status == DownloadStatus.Completed),
                    _history.Count(r => r.Status == DownloadStatus.Failed),
                    _history.Count(r => r.Status == DownloadStatus.Downloading),
                    _history.Where(r => r.Status == DownloadStatus.Completed).Sum(r => r.DownloadedSize)
                );
            }
        }

        private List<DownloadRecord> LoadHistory()
        {
            try
            {
                if (File.Exists(_historyPath))
                {
                    var json = File.ReadAllText(_historyPath);
                    return JsonSerializer.Deserialize<List<DownloadRecord>>(json, JsonOptions) ?? [];
                }
            }
            catch
            {
                // ignore
            }

            return [];
        }

        private void SaveHistory()
        {
            var json = JsonSerializer.Serialize(_history, JsonOptions);
            File.WriteAllText(_historyPath, json);
        }
    }
}
