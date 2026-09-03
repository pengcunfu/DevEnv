using System.ComponentModel;

namespace ProgramBox.Models
{
    /// <summary>
    /// 开发环境服务信息（MySQL / Redis / PostgreSQL），进程模式控制
    /// </summary>
    public class DevService : INotifyPropertyChanged
    {
        /// <summary>
        /// 唯一标识
        /// </summary>
        public string Key { get; init; } = string.Empty;

        /// <summary>
        /// 显示名称
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// 安装目录
        /// </summary>
        public string InstallPath { get; init; } = string.Empty;

        /// <summary>
        /// 数据目录
        /// </summary>
        public string DataPath { get; init; } = string.Empty;

        /// <summary>
        /// 用于检测运行状态的进程名（不含扩展名）
        /// </summary>
        public string ProcessName { get; init; } = string.Empty;

        /// <summary>
        /// 启动程序完整路径
        /// </summary>
        public string StartExe { get; init; } = string.Empty;

        /// <summary>
        /// 启动参数
        /// </summary>
        public string StartArgs { get; init; } = string.Empty;

        /// <summary>
        /// 停止程序完整路径（为空则直接结束进程）
        /// </summary>
        public string StopExe { get; init; } = string.Empty;

        /// <summary>
        /// 停止参数
        /// </summary>
        public string StopArgs { get; init; } = string.Empty;

        private bool _running;

        /// <summary>
        /// 服务是否正在运行
        /// </summary>
        public bool Running
        {
            get => _running;
            set
            {
                if (_running == value)
                    return;
                _running = value;
                OnPropertyChanged(nameof(Running));
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(IsStopped));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(ActionButtonText));
            }
        }

        private bool _isBusy;

        /// <summary>
        /// 是否正在执行启动/停止操作
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value)
                    return;
                _isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(CanAction));
            }
        }

        /// <summary>
        /// 是否在运行（样式类绑定用）
        /// </summary>
        public bool IsRunning => Running;

        /// <summary>
        /// 是否已停止（样式类绑定用）
        /// </summary>
        public bool IsStopped => !Running;

        /// <summary>
        /// 状态文本
        /// </summary>
        public string StatusText => Running ? "运行中" : "已停止";

        /// <summary>
        /// 操作按钮文本
        /// </summary>
        public string ActionButtonText => Running ? "停止" : "启动";

        /// <summary>
        /// 操作按钮是否可用
        /// </summary>
        public bool CanAction => !IsBusy;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
