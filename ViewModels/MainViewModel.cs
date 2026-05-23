using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using DevEnv.Models;
using DevEnv.Services;

namespace DevEnv.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ProcessManager _processManager;

        public MainViewModel()
        {
            _processManager = AppServices.ProcessManager;
            Processes = new ObservableCollection<ProcessItemViewModel>();

            foreach (var def in _processManager.GetDefinitions())
                Processes.Add(new ProcessItemViewModel(def, _processManager));

            _processManager.ProcessStatusUpdated += OnProcessStatusUpdated;
            _processManager.StartMonitoring();
        }

        public ObservableCollection<ProcessItemViewModel> Processes { get; }

        public string AppsDirectory => _processManager.GetAppsDirectory();

        private void OnProcessStatusUpdated(object? sender, ProcessStatusUpdatedEventArgs e)
        {
            var item = Processes.FirstOrDefault(p => p.Name == e.ProcessName);
            if (item != null)
            {
                Application.Current.Dispatcher.Invoke(() => item.UpdateStatus(e.Status));
            }
        }

        public void StopMonitoring() => _processManager.StopMonitoring();

        public void OpenAppsDirectory()
        {
            var dir = _processManager.GetAppsDirectory();
            Directory.CreateDirectory(dir);
            Process.Start("explorer.exe", dir);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ProcessItemViewModel : INotifyPropertyChanged
    {
        private readonly ProcessManager _processManager;
        private string _statusText = "检查中...";
        private string _statusColor = "Gray";
        private bool _canStart;
        private bool _canStop;

        public ProcessItemViewModel(ProcessDefinition definition, ProcessManager processManager)
        {
            Name = definition.Name;
            DisplayName = definition.DisplayName;
            Description = definition.Description;
            _processManager = processManager;
        }

        public string Name { get; }
        public string DisplayName { get; }
        public string Description { get; }

        public string StatusText
        {
            get => _statusText;
            set { if (_statusText != value) { _statusText = value; OnPropertyChanged(); } }
        }

        public string StatusColor
        {
            get => _statusColor;
            set { if (_statusColor != value) { _statusColor = value; OnPropertyChanged(); } }
        }

        public bool CanStart
        {
            get => _canStart;
            set { if (_canStart != value) { _canStart = value; OnPropertyChanged(); } }
        }

        public bool CanStop
        {
            get => _canStop;
            set { if (_canStop != value) { _canStop = value; OnPropertyChanged(); } }
        }

        public void UpdateStatus(ProcessStatusInfo status)
        {
            StatusText = status.DisplayText;
            StatusColor = status.Color;

            switch (status.Status)
            {
                case ProcessState.Running:
                case ProcessState.ExternalRunning:
                    CanStart = false;
                    CanStop = true;
                    break;
                case ProcessState.Stopped:
                    CanStart = true;
                    CanStop = false;
                    break;
                case ProcessState.NotConfigured:
                    CanStart = false;
                    CanStop = false;
                    break;
                default:
                    CanStart = true;
                    CanStop = true;
                    break;
            }
        }

        public async Task StartAsync()
        {
            StatusText = "启动中...";
            StatusColor = "Blue";
            CanStart = false;
            CanStop = false;

            var (success, message) = await _processManager.StartProcessAsync(Name);
            ShowMessage(success, message);
        }

        public async Task StopAsync()
        {
            StatusText = "停止中...";
            StatusColor = "Orange";
            CanStart = false;
            CanStop = false;

            var (success, message) = await _processManager.StopProcessAsync(Name);
            ShowMessage(success, message);
        }

        private static void ShowMessage(bool success, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, success ? "操作成功" : "操作失败",
                    MessageBoxButton.OK, success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
