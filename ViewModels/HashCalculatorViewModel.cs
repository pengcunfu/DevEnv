using DevEnv.UI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using DevEnv.Services;
using DevEnv.Commands;

namespace DevEnv.ViewModels
{
    public class HashCalculatorViewModel : INotifyPropertyChanged
    {
        private readonly HashCalculatorService _hashCalculator;
        private string _filePath = string.Empty;
        private string _textInput = string.Empty;
        private string _selectedHashType = "SHA-256";
        private int _progress;
        private string _progressText = string.Empty;
        private string _statusText = "准备就绪";
        private bool _isCalculating;
        private ObservableCollection<HashResult> _hashResults = new();

        public HashCalculatorViewModel()
        {
            _hashCalculator = new HashCalculatorService();
            InitializeCommands();
        }

        #region Properties

        public string FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value);
        }

        public string TextInput
        {
            get => _textInput;
            set => SetProperty(ref _textInput, value);
        }

        public string SelectedHashType
        {
            get => _selectedHashType;
            set => SetProperty(ref _selectedHashType, value);
        }

        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public bool IsCalculating
        {
            get => _isCalculating;
            set
            {
                if (SetProperty(ref _isCalculating, value))
                {
                    OnPropertyChanged(nameof(CanCalculate));
                }
            }
        }

        public bool CanCalculate => !IsCalculating;

        public ObservableCollection<HashResult> HashResults
        {
            get => _hashResults;
            set => SetProperty(ref _hashResults, value);
        }

        #endregion

        #region Commands

        public ICommand BrowseFileCommand { get; private set; }
        public ICommand PastePathCommand { get; private set; }
        public ICommand CalculateHashCommand { get; private set; }
        public ICommand CalculateAllHashesCommand { get; private set; }
        public ICommand CalculateTextHashCommand { get; private set; }
        public ICommand CopyHashCommand { get; private set; }

        [MemberNotNull(nameof(BrowseFileCommand), nameof(PastePathCommand), nameof(CalculateHashCommand), nameof(CalculateAllHashesCommand), nameof(CalculateTextHashCommand), nameof(CopyHashCommand))]
        private void InitializeCommands()
        {
            BrowseFileCommand = new RelayCommand(async () => await BrowseFileAsync(), () => CanCalculate);
            PastePathCommand = new RelayCommand(PasteFilePath, () => CanCalculate);
            CalculateHashCommand = new RelayCommand(async () => await CalculateSingleHashAsync(), () => CanCalculate && !string.IsNullOrEmpty(FilePath));
            CalculateAllHashesCommand = new RelayCommand(async () => await CalculateAllHashesAsync(), () => CanCalculate && !string.IsNullOrEmpty(FilePath));
            CalculateTextHashCommand = new RelayCommand(async () => await CalculateTextHashAsync(), () => CanCalculate && !string.IsNullOrEmpty(TextInput));
            CopyHashCommand = new RelayCommand<string>(CopyHashToClipboard);
        }

        #endregion

        #region Command Implementations

        private async Task BrowseFileAsync()
        {
            try
            {
                var files = await FileDialogs.OpenFilesAsync(
                    WindowDialogHelper.GetMainWindow(),
                    "选择要计算哈希的文件",
                    allowMultiple: false);

                if (files is { Length: > 0 })
                {
                    FilePath = files[0];
                    StatusText = $"已选择文件: {Path.GetFileName(FilePath)}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"浏览文件时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PasteFilePath()
        {
            try
            {
                var text = ClipboardHelper.GetText();
                if (!string.IsNullOrEmpty(text))
                {
                    // 移除引号（如果存在）
                    text = text.Trim('"', '\'');

                    if (File.Exists(text))
                    {
                        FilePath = text;
                        StatusText = $"已粘贴文件路径: {Path.GetFileName(FilePath)}";
                    }
                    else
                    {
                        MessageBox.Show("剪贴板中的文件路径不存在", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    MessageBox.Show("剪贴板为空", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"粘贴路径时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CalculateSingleHashAsync()
        {
            if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath))
            {
                MessageBox.Show("请先选择一个有效的文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsCalculating = true;
                HashResults.Clear();
                ShowProgress(true);

                var hashType = ParseHashType(SelectedHashType);
                StatusText = $"正在计算 {hashType} 哈希值...";

                var result = await _hashCalculator.CalculateFileHashAsync(FilePath, hashType, new Progress<int>(progress =>
                {
                    Progress = progress;
                    ProgressText = $"进度: {progress}%";
                }));

                HashResults.Add(result);
                StatusText = $"计算完成! 文件大小: {HashCalculatorService.FormatFileSize(result.FileSize)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"计算哈希时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText = "计算失败";
            }
            finally
            {
                IsCalculating = false;
                ShowProgress(false);
            }
        }

        private async Task CalculateAllHashesAsync()
        {
            if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath))
            {
                MessageBox.Show("请先选择一个有效的文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsCalculating = true;
                HashResults.Clear();
                ShowProgress(true);

                var hashTypes = new[] { HashType.MD5, HashType.SHA1, HashType.SHA256, HashType.SHA384, HashType.SHA512 };
                StatusText = "正在计算所有哈希算法...";

                var results = await _hashCalculator.CalculateMultipleHashesAsync(FilePath, hashTypes, new Progress<int>(progress =>
                {
                    Progress = progress;
                    ProgressText = $"进度: {progress}%";
                }));

                foreach (var result in results)
                {
                    HashResults.Add(result);
                }

                StatusText = $"全部计算完成! 文件大小: {HashCalculatorService.FormatFileSize(results[0].FileSize)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"计算哈希时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText = "计算失败";
            }
            finally
            {
                IsCalculating = false;
                ShowProgress(false);
            }
        }

        private async Task CalculateTextHashAsync()
        {
            if (string.IsNullOrEmpty(TextInput))
            {
                MessageBox.Show("请先输入文本内容", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsCalculating = true;
                HashResults.Clear();
                ShowProgress(true);

                var hashType = ParseHashType(SelectedHashType);
                StatusText = $"正在计算文本的 {hashType} 哈希值...";
                ProgressText = "计算中...";
                Progress = 50;

                var result = await _hashCalculator.CalculateTextHashAsync(TextInput, hashType);

                Progress = 100;
                ProgressText = "完成!";
                HashResults.Add(result);
                StatusText = $"文本哈希计算完成! 文本长度: {TextInput.Length} 字符";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"计算文本哈希时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText = "计算失败";
            }
            finally
            {
                IsCalculating = false;
                ShowProgress(false);
            }
        }

        private void CopyHashToClipboard(string hash)
        {
            try
            {
                if (!string.IsNullOrEmpty(hash))
                {
                    ClipboardHelper.SetText(hash);
                    StatusText = "哈希值已复制到剪贴板";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制到剪贴板时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Helper Methods

        private HashType ParseHashType(string hashTypeString)
        {
            return hashTypeString switch
            {
                "MD5" => HashType.MD5,
                "SHA-1" => HashType.SHA1,
                "SHA-256" => HashType.SHA256,
                "SHA-384" => HashType.SHA384,
                "SHA-512" => HashType.SHA512,
                _ => HashType.SHA256
            };
        }

        private void ShowProgress(bool show)
        {
            Progress = 0;
            ProgressText = show ? "准备计算..." : string.Empty;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}

