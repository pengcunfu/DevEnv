using DevEnv.Views;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DevEnv.Models;

namespace DevEnv.ViewModels
{
    public class HostsFileEditViewModel : INotifyPropertyChanged
    {
        private const string HostsFilePath = @"C:\Windows\System32\drivers\etc\hosts";

        private ObservableCollection<HostsEntry> _entries = new();
        private string _hostsText = string.Empty;
        private int _selectedTabIndex;
        private HostsEntry? _selectedEntry;

        public ObservableCollection<HostsEntry> Entries
        {
            get => _entries;
            set
            {
                if (_entries != value)
                {
                    _entries = value;
                    OnPropertyChanged();
                }
            }
        }

        public string HostsText
        {
            get => _hostsText;
            set
            {
                if (_hostsText != value)
                {
                    _hostsText = value;
                    OnPropertyChanged();
                }
            }
        }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (_selectedTabIndex != value)
                {
                    _selectedTabIndex = value;
                    OnPropertyChanged();

                    // 当切换到文本编辑标签页时，重新加载文本
                    if (value == 0)
                    {
                        LoadHostsText();
                    }
                }
            }
        }

        public HostsEntry? SelectedEntry
        {
            get => _selectedEntry;
            set
            {
                if (_selectedEntry != value)
                {
                    _selectedEntry = value;
                    OnPropertyChanged();
                }
            }
        }

        // 命令
        public ICommand AddEntryCommand { get; }
        public ICommand ModifyEntryCommand { get; }
        public ICommand DeleteEntryCommand { get; }
        public ICommand ReloadCommand { get; }
        public ICommand SaveTextCommand { get; }
        public ICommand LocateFileCommand { get; }
        public ICommand OpenInNotepadCommand { get; }
        public ICommand OpenEnvVarsCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public HostsFileEditViewModel()
        {
            AddEntryCommand = new RelayCommand(async () => await AddEntry());
            ModifyEntryCommand = new RelayCommand(async () => await ModifyEntry(), () => SelectedEntry != null);
            DeleteEntryCommand = new RelayCommand(async () => await DeleteEntry(), () => SelectedEntry != null);
            ReloadCommand = new RelayCommand(async () => await LoadHostsFile());
            SaveTextCommand = new RelayCommand(async () => await SaveText());
            LocateFileCommand = new RelayCommand(() => LocateFile());
            OpenInNotepadCommand = new RelayCommand(() => OpenInNotepad());
            OpenEnvVarsCommand = new RelayCommand(() => OpenEnvVars());

            // 初始化时加载文件
            _ = LoadHostsFile();
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            // 更新命令的 CanExecute
            if (propertyName == nameof(SelectedEntry))
            {
                (ModifyEntryCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeleteEntryCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public async Task LoadHostsFile()
        {
            try
            {
                await Task.Run(() =>
                {
                    if (!File.Exists(HostsFilePath))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show("找不到 hosts 文件", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                        });
                        return;
                    }

                    var content = File.ReadAllText(HostsFilePath);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        HostsText = content;
                        ParseHostsContent(content);
                    });
                });
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("没有权限读取 hosts 文件，请以管理员身份运行", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取 hosts 文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ParseHostsContent(string content)
        {
            Entries.Clear();

            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (HostsEntry.TryParse(line, out var entry))
                {
                    entry.LineNumber = i;
                    Entries.Add(entry);
                }
            }
        }

        private void LoadHostsText()
        {
            // 从条目重新生成文本内容
            var lines = HostsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var newLines = lines.ToList();

            // 保留注释和空行
            var validLines = newLines.Where(line =>
                string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#")).ToList();

            // 添加条目
            foreach (var entry in Entries)
            {
                validLines.Add($"{entry.Ip}\t{entry.Domain}");
            }

            HostsText = string.Join(Environment.NewLine, validLines);
        }

        public async Task SaveText()
        {
            try
            {
                await Task.Run(() =>
                {
                    // 创建备份
                    var backupPath = HostsFilePath + ".backup";
                    if (File.Exists(HostsFilePath))
                    {
                        File.Copy(HostsFilePath, backupPath, true);
                    }

                    File.WriteAllText(HostsFilePath, HostsText);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("hosts 文件已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        // 重新解析
                        ParseHostsContent(HostsText);
                    });
                });
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("没有权限写入 hosts 文件，请以管理员身份运行", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存 hosts 文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task SaveEntries()
        {
            try
            {
                await Task.Run(() =>
                {
                    // 读取原始文件
                    var lines = File.ReadAllLines(HostsFilePath);
                    var newLines = lines.Where(line =>
                        string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#")).ToList();

                    // 添加新的条目
                    foreach (var entry in Entries)
                    {
                        newLines.Add($"{entry.Ip}\t{entry.Domain}");
                    }

                    // 创建备份
                    var backupPath = HostsFilePath + ".backup";
                    File.Copy(HostsFilePath, backupPath, true);

                    // 写入文件
                    File.WriteAllLines(HostsFilePath, newLines);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("hosts 文件已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadHostsText();
                    });
                });
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("没有权限写入 hosts 文件，请以管理员身份运行", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存 hosts 文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task AddEntry()
        {
            var dialog = new EditHostsEntryDialog();
            if (dialog.ShowDialog() == true)
            {
                var entry = new HostsEntry
                {
                    Ip = dialog.Ip,
                    Domain = dialog.Domain
                };
                Entries.Add(entry);
                await SaveEntries();
            }
        }

        public async Task ModifyEntry()
        {
            if (SelectedEntry == null) return;

            var dialog = new EditHostsEntryDialog(SelectedEntry.Ip, SelectedEntry.Domain);
            if (dialog.ShowDialog() == true)
            {
                SelectedEntry.Ip = dialog.Ip;
                SelectedEntry.Domain = dialog.Domain;
                OnPropertyChanged(nameof(SelectedEntry));
                await SaveEntries();
            }
        }

        public async Task DeleteEntry()
        {
            if (SelectedEntry == null) return;

            var result = MessageBox.Show("确定要删除选中的条目吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                Entries.Remove(SelectedEntry);
                await SaveEntries();
            }
        }

        private void LocateFile()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{HostsFilePath}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法定位到文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenInNotepad()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = HostsFilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法在记事本中打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenEnvVars()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = "sysdm.cpl,EditEnvironmentVariables",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开环境变量设置: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }

        public void Execute(object? parameter)
        {
            _execute();
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
