using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using DevEnv.Services;

namespace DevEnv.ViewModels
{
    public class ImageConverterViewModel : INotifyPropertyChanged
    {
        private readonly ImageConversionService _conversionService;
        private ObservableCollection<string> _selectedFiles;
        private string _outputDirectory;
        private int _quality = 95;
        private bool _useSourceDirectory;
        private bool _overwriteExisting;
        private string _logText;
        private int _successCount;
        private int _errorCount;
        private double _progress;
        private bool _isConverting;
        private string _selectedOutputFormat;

        public ImageConverterViewModel()
        {
            _conversionService = new ImageConversionService();
            _selectedFiles = new ObservableCollection<string>();
            _logText = string.Empty;
            _selectedOutputFormat = "JPEG (.jpg)";

            // 设置默认输出目录
            _outputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ConvertedImages");
        }

        public ObservableCollection<string> SelectedFiles
        {
            get => _selectedFiles;
            set
            {
                _selectedFiles = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedFilesText));
                OnPropertyChanged(nameof(CanStartConversion));
            }
        }

        public string SelectedFilesText
        {
            get
            {
                if (_selectedFiles.Count == 0)
                    return "未选择文件";
                else if (_selectedFiles.Count == 1)
                    return $"已选择 1 个文件";
                else
                    return $"已选择 {_selectedFiles.Count} 个文件";
            }
        }

        public string OutputDirectory
        {
            get => _outputDirectory;
            set
            {
                _outputDirectory = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStartConversion));
            }
        }

        public int Quality
        {
            get => _quality;
            set
            {
                _quality = value;
                OnPropertyChanged();
            }
        }

        public bool UseSourceDirectory
        {
            get => _useSourceDirectory;
            set
            {
                _useSourceDirectory = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStartConversion));
            }
        }

        public bool OverwriteExisting
        {
            get => _overwriteExisting;
            set
            {
                _overwriteExisting = value;
                OnPropertyChanged();
            }
        }

        public string LogText
        {
            get => _logText;
            set
            {
                _logText = value;
                OnPropertyChanged();
            }
        }

        public int SuccessCount
        {
            get => _successCount;
            set
            {
                _successCount = value;
                OnPropertyChanged();
            }
        }

        public int ErrorCount
        {
            get => _errorCount;
            set
            {
                _errorCount = value;
                OnPropertyChanged();
            }
        }

        public string ProgressText => IsProgressVisible ? $"{Progress:F1}%" : "未开始";

        public double Progress
        {
            get => _progress;
            set
            {
                _progress = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProgressText));
            }
        }

        public bool IsProgressVisible => _isConverting || Progress > 0;

        public string SelectedOutputFormat
        {
            get => _selectedOutputFormat;
            set
            {
                _selectedOutputFormat = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsQualityVisible));
            }
        }

        public bool IsQualityVisible
        {
            get
            {
                var format = ExtractFormat(SelectedOutputFormat);
                var formatsWithQuality = new[] { "JPEG", "WEBP" };
                return formatsWithQuality.Contains(format.ToUpperInvariant());
            }
        }

        public bool CanStartConversion
        {
            get
            {
                return _selectedFiles.Count > 0 &&
                       (!_useSourceDirectory || !string.IsNullOrEmpty(_outputDirectory)) &&
                       !_isConverting;
            }
        }

        public void AddFiles(string[] files)
        {
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif", ".webp", ".ico", ".jfif", ".avif", ".heic", ".heif", ".svg" };

            foreach (var file in files)
            {
                if (imageExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                {
                    if (!_selectedFiles.Contains(file))
                    {
                        _selectedFiles.Add(file);
                    }
                }
                else
                {
                    AppendLog($"跳过不支持的文件格式: {Path.GetFileName(file)}");
                }
            }

            OnPropertyChanged(nameof(SelectedFilesText));
            OnPropertyChanged(nameof(CanStartConversion));
        }

        public void AddFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                AppendLog($"文件夹不存在: {folderPath}");
                return;
            }

            var imageExtensions = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif", "*.tiff", "*.tif", "*.webp", "*.ico", "*.jfif", "*.avif", "*.heic", "*.heif", "*.svg" };
            var files = imageExtensions.SelectMany(ext => Directory.GetFiles(folderPath, ext, SearchOption.AllDirectories)).ToArray();

            if (files.Length > 0)
            {
                AddFiles(files);
                AppendLog($"从文件夹添加了 {files.Length} 个图像文件");
            }
            else
            {
                AppendLog("文件夹中未找到支持的图像文件");
            }
        }

        public void UpdateOutputFormat(string format)
        {
            SelectedOutputFormat = format;
        }

        public void ClearLog()
        {
            LogText = string.Empty;
            SuccessCount = 0;
            ErrorCount = 0;
            Progress = 0;
        }

        public async Task StartConversionAsync()
        {
            _isConverting = true;
            SuccessCount = 0;
            ErrorCount = 0;
            Progress = 0;

            OnPropertyChanged(nameof(CanStartConversion));
            OnPropertyChanged(nameof(IsProgressVisible));

            try
            {
                AppendLog("开始转换图像...");
                AppendLog($"输入文件数量: {_selectedFiles.Count}");
                AppendLog($"输出格式: {_selectedOutputFormat}");
                AppendLog($"质量设置: {_quality}%");

                var format = ExtractFormat(_selectedOutputFormat);
                var outputDir = UseSourceDirectory ? null : OutputDirectory;

                var progress = new Progress<ImageConversionProgress>(p =>
                {
                    Progress = p.ProgressPercentage;
                    AppendLog($"正在转换: {Path.GetFileName(p.CurrentFile)} ({p.ProcessedFiles}/{p.TotalFiles})");
                });

                var results = await _conversionService.ConvertImagesAsync(
                    _selectedFiles.ToList(),
                    format,
                    outputDir,
                    Quality,
                    OverwriteExisting,
                    progress);

                // 统计结果
                foreach (var result in results)
                {
                    if (result.Success)
                    {
                        SuccessCount++;
                        AppendLog($"✓ 转换成功: {Path.GetFileName(result.OutputFile)}");
                    }
                    else
                    {
                        ErrorCount++;
                        AppendLog($"✗ 转换失败: {Path.GetFileName(result.InputFile)} - {result.ErrorMessage}");
                    }
                }

                Progress = 100;
                AppendLog("转换完成!");
            }
            catch (Exception ex)
            {
                ErrorCount++;
                AppendLog($"转换过程发生错误: {ex.Message}");
            }
            finally
            {
                _isConverting = false;
                OnPropertyChanged(nameof(CanStartConversion));
                OnPropertyChanged(nameof(IsProgressVisible));
            }
        }

        private void AppendLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogText += $"[{timestamp}] {message}{Environment.NewLine}";
        }

        private string ExtractFormat(string formatString)
        {
            if (string.IsNullOrEmpty(formatString))
                return "JPEG";

            // 从格式字符串中提取格式，例如从 "JPEG (.jpg)" 提取 "JPEG"
            var parts = formatString.Split(' ');
            return parts.FirstOrDefault()?.Replace(".", "").ToUpperInvariant() ?? "JPEG";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
