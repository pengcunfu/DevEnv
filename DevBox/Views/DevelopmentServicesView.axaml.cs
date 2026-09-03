using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ProgramBox.Models;
using ProgramBox.Utils;

namespace ProgramBox.Views
{
    /// <summary>
    /// 开发环境 Tab：以进程模式控制 MySQL / Redis / PostgreSQL 的启动与停止
    /// </summary>
    public partial class DevelopmentServicesView : UserControl
    {
        private readonly IReadOnlyList<DevService> _services;
        private readonly DispatcherTimer _statusTimer;

        public DevelopmentServicesView()
        {
            InitializeComponent();
            _services = DevServicesManager.Services;

            ServiceItemsControl.ItemsSource = _services;

            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _statusTimer.Tick += (_, _) => RefreshStatus();
            _statusTimer.Start();

            RefreshStatus();
        }

        /// <summary>
        /// 刷新所有服务的运行状态
        /// </summary>
        private void RefreshStatus()
        {
            foreach (var service in _services)
                service.Running = DevServicesManager.IsRunning(service);
        }

        /// <summary>
        /// 启动 / 停止服务
        /// </summary>
        private async void ActionButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: DevService service })
                return;

            if (service.IsBusy)
                return;

            service.IsBusy = true;
            try
            {
                if (service.Running)
                {
                    await Task.Run(() => DevServicesManager.Stop(service));
                    await AppHelper.ShowInfoAsync($"{service.Name} 已停止");
                }
                else
                {
                    await Task.Run(() => DevServicesManager.Start(service));
                    await AppHelper.ShowInfoAsync($"{service.Name} 已启动");
                }
            }
            catch (Exception ex)
            {
                await AppHelper.ShowErrorAsync($"操作 {service.Name} 失败: {ex.Message}");
            }
            finally
            {
                service.IsBusy = false;
                RefreshStatus();
            }
        }

        /// <summary>
        /// 打开服务的数据目录
        /// </summary>
        private void OpenDataDir_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: DevService service })
                return;

            AppHelper.OpenInExplorer(service.DataPath);
        }
    }
}
