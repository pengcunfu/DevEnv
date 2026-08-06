using System;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using ProgramBox.Utils;

namespace ProgramBox.Views
{
    public partial class DevelopmentToolsView : UserControl
    {
        private readonly JsonDataManager _dataManager;

        public DevelopmentToolsView(JsonDataManager dataManager)
        {
            InitializeComponent();
            _dataManager = dataManager;
            LoadEnvironments();
        }

        /// <summary>
        /// 加载开发环境
        /// </summary>
        private void LoadEnvironments()
        {
            try
            {
                EnvironmentItemsControl.ItemsSource = _dataManager.Data.InsAtomList;
                
                // 显示/隐藏空状态
                EmptyState.IsVisible = _dataManager.Data.InsAtomList.Count == 0;
            }
            catch (Exception ex)
            {
                _ = AppHelper.ShowErrorAsync($"加载开发环境失败: {ex.Message}");
            }
        }

        #region 事件处理

        private void AddEnvironmentButton_Click(object? sender, RoutedEventArgs e)
        {
            _ = AppHelper.ShowInfoAsync("添加环境功能正在开发中...");
        }

        private void EnvironmentCard_Click(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.Tag is string key)
            {
                OpenEnvironmentDetails(key);
            }
        }

        private void ConfigureEnvironment_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string key)
            {
                _ = AppHelper.ShowInfoAsync($"{key} 环境配置功能正在开发中...");
            }
            e.Handled = true;
        }

        private void MoreActions_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string key)
            {
                ShowEnvironmentContextMenu(button, key);
            }
            e.Handled = true;
        }

        private void OpenTerminal_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string key)
            {
                OpenEnvironmentTerminal(key);
            }
            e.Handled = true;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 打开环境详情
        /// </summary>
        private void OpenEnvironmentDetails(string key)
        {
            _ = AppHelper.ShowInfoAsync($"环境 {key} 的详细配置界面正在开发中...");
        }

        /// <summary>
        /// 显示环境上下文菜单
        /// </summary>
        private void ShowEnvironmentContextMenu(Button button, string key)
        {
            var menu = new ContextMenu();
            
            var openItem = new MenuItem { Header = "打开详情" };
            openItem.Click += (s, e) => OpenEnvironmentDetails(key);
            menu.Items.Add(openItem);
            
            var configItem = new MenuItem { Header = "配置" };
            configItem.Click += (s, e) => _ = AppHelper.ShowInfoAsync($"{key} 环境配置功能正在开发中...");
            menu.Items.Add(configItem);
            
            menu.Items.Add(new Separator());
            
            var terminalItem = new MenuItem { Header = "打开终端" };
            terminalItem.Click += (s, e) => OpenEnvironmentTerminal(key);
            menu.Items.Add(terminalItem);
            
            var explorerItem = new MenuItem { Header = "在资源管理器中打开" };
            explorerItem.Click += (s, e) => OpenEnvironmentInExplorer(key);
            menu.Items.Add(explorerItem);
            
            menu.Items.Add(new Separator());
            
            var removeItem = new MenuItem { Header = "移除" };
            removeItem.Click += (s, e) => RemoveEnvironment(key);
            menu.Items.Add(removeItem);
            
            menu.Open(button);
        }

        /// <summary>
        /// 打开环境终端
        /// </summary>
        private void OpenEnvironmentTerminal(string key)
        {
            try
            {
                string command = key.ToLower() switch
                {
                    "node" => "node --version",
                    "python" => "python --version",
                    "java" => "java -version",
                    "php" => "php --version",
                    "cpp" => "gcc --version",
                    _ => "echo 环境信息"
                };

                // 打开PowerShell并执行命令
                var processInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoExit -Command \"{command}\"",
                    UseShellExecute = true
                };

                Process.Start(processInfo);
            }
            catch (Exception ex)
            {
                _ = AppHelper.ShowErrorAsync($"打开终端失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 在资源管理器中打开环境
        /// </summary>
        private void OpenEnvironmentInExplorer(string key)
        {
            var envPath = GetEnvironmentPath(key);
            if (!string.IsNullOrEmpty(envPath))
            {
                AppHelper.OpenInExplorer(envPath);
            }
            else
            {
                _ = AppHelper.ShowWarningAsync("未找到环境路径");
            }
        }

        /// <summary>
        /// 获取环境路径
        /// </summary>
        private string GetEnvironmentPath(string key)
        {
            return key.ToLower() switch
            {
                "java" => @"C:\Program Files\Java",
                "node" => @"C:\Program Files\nodejs",
                "python" => @"C:\Python",
                _ => _dataManager.Data.Config.BaseEnvPath
            };
        }

        /// <summary>
        /// 移除环境
        /// </summary>
        private async void RemoveEnvironment(string key)
        {
            if (await AppHelper.ShowConfirmAsync($"确定要移除 {key} 环境吗？"))
            {
                var environment = _dataManager.Data.InsAtomList.FirstOrDefault(e => e.Key == key);
                if (environment != null)
                {
                    _dataManager.Data.InsAtomList.Remove(environment);
                    _dataManager.Save();
                    LoadEnvironments();
                    await AppHelper.ShowInfoAsync("环境已移除");
                }
            }
        }

        #endregion
    }
}
