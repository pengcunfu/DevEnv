using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using DevEnv.Services;
using DevEnv.Models;

namespace DevEnv.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ServiceManager _serviceManager;
        private readonly List<string> _services;

        public MainViewModel()
        {
            _serviceManager = new ServiceManager();
            _services = new List<string> { "MySQL", "Redis", "MongoDB", "MinIO" };
            Services = new ObservableCollection<ServiceItemViewModel>();

            InitializeServices();
            _serviceManager.ServiceStatusUpdated += OnServiceStatusUpdated;
            _serviceManager.StartMonitoring();
        }

        public ObservableCollection<ServiceItemViewModel> Services { get; }

        private void InitializeServices()
        {
            foreach (var serviceName in _services)
            {
                Services.Add(new ServiceItemViewModel(serviceName, _serviceManager));
            }
        }

        private void OnServiceStatusUpdated(object? sender, ServiceStatusUpdatedEventArgs e)
        {
            var serviceViewModel = Services.FirstOrDefault(s => s.Name == e.ServiceName);
            if (serviceViewModel != null)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    serviceViewModel.UpdateStatus(e.Status);
                });
            }
        }

        public void StopMonitoring()
        {
            _serviceManager.StopMonitoring();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ServiceItemViewModel : INotifyPropertyChanged
    {
        private readonly ServiceManager _serviceManager;
        private string _statusText = "检查中...";
        private string _statusColor = "Gray";
        private bool _canStart = false;
        private bool _canStop = false;

        public ServiceItemViewModel(string name, ServiceManager serviceManager)
        {
            Name = name;
            _serviceManager = serviceManager;
        }

        public string Name { get; }

        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string StatusColor
        {
            get => _statusColor;
            set
            {
                if (_statusColor != value)
                {
                    _statusColor = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool CanStart
        {
            get => _canStart;
            set
            {
                if (_canStart != value)
                {
                    _canStart = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool CanStop
        {
            get => _canStop;
            set
            {
                if (_canStop != value)
                {
                    _canStop = value;
                    OnPropertyChanged();
                }
            }
        }

        public void UpdateStatus(ServiceStatusInfo status)
        {
            StatusText = status.DisplayText;
            StatusColor = status.Color;

            switch (status.Status)
            {
                case ServiceState.Running:
                    CanStart = false;
                    CanStop = true;
                    break;
                case ServiceState.Stopped:
                    CanStart = true;
                    CanStop = false;
                    break;
                case ServiceState.NotFound:
                    CanStart = false;
                    CanStop = false;
                    break;
                default:
                    CanStart = true;
                    CanStop = true;
                    break;
            }
        }

        public async Task StartServiceAsync()
        {
            StatusText = "启动中...";
            StatusColor = "Blue";
            CanStart = false;
            CanStop = false;

            var (success, message) = await _serviceManager.StartServiceAsync(Name);

            // Status will be updated by the timer, just show message
            await ShowMessage(success, message);
        }

        public async Task StopServiceAsync()
        {
            StatusText = "停止中...";
            StatusColor = "Orange";
            CanStart = false;
            CanStop = false;

            var (success, message) = await _serviceManager.StopServiceAsync(Name);

            // Status will be updated by the timer, just show message
            await ShowMessage(success, message);
        }

        private async Task ShowMessage(bool success, string message)
        {
            await Task.Run(() =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    var messageBoxImage = success ? MessageBoxImage.Information : MessageBoxImage.Warning;
                    var messageBoxTitle = success ? "操作成功" : "操作失败";
                    MessageBox.Show(message, messageBoxTitle, MessageBoxButton.OK, messageBoxImage);
                });
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}