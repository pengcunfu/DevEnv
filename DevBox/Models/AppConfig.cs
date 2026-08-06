using System.ComponentModel;

namespace ProgramBox.Models
{
    /// <summary>
    /// 应用程序配置类
    /// </summary>
    public class AppConfig : INotifyPropertyChanged
    {
        private bool _trayIcon = true;
        private bool _selfStarting = false;
        private string _baseEnvPath = @"D:\ProgramBox\env";
        private string _language = "zh-CN";
        private bool _darkMode = false;

        /// <summary>
        /// 托盘图标显示
        /// </summary>
        public bool TrayIcon
        {
            get => _trayIcon;
            set
            {
                _trayIcon = value;
                OnPropertyChanged(nameof(TrayIcon));
            }
        }

        /// <summary>
        /// 开机自启动
        /// </summary>
        public bool SelfStarting
        {
            get => _selfStarting;
            set
            {
                _selfStarting = value;
                OnPropertyChanged(nameof(SelfStarting));
            }
        }

        /// <summary>
        /// 环境基础路径
        /// </summary>
        public string BaseEnvPath
        {
            get => _baseEnvPath;
            set
            {
                _baseEnvPath = value;
                OnPropertyChanged(nameof(BaseEnvPath));
            }
        }

        /// <summary>
        /// 界面语言
        /// </summary>
        public string Language
        {
            get => _language;
            set
            {
                _language = value;
                OnPropertyChanged(nameof(Language));
            }
        }

        /// <summary>
        /// 深色模式
        /// </summary>
        public bool DarkMode
        {
            get => _darkMode;
            set
            {
                _darkMode = value;
                OnPropertyChanged(nameof(DarkMode));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 环境配置信息
    /// </summary>
    public class EnvironmentInfo : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _version = string.Empty;
        private string _path = string.Empty;
        private bool _isActive = false;
        private string _description = string.Empty;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public string Version
        {
            get => _version;
            set
            {
                _version = value;
                OnPropertyChanged(nameof(Version));
            }
        }

        public string Path
        {
            get => _path;
            set
            {
                _path = value;
                OnPropertyChanged(nameof(Path));
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                OnPropertyChanged(nameof(IsActive));
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged(nameof(Description));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
