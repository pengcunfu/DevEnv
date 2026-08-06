using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using ProgramBox.Utils;
using ProgramBox.Views;

namespace ProgramBox
{
    public partial class MainWindow : Window
    {
        private readonly JsonDataManager _dataManager;
        private Button? _currentActiveButton;

        public MainWindow()
        {
            InitializeComponent();
            _dataManager = new JsonDataManager();
            VersionText.Text = $"版本 {VersionInfo.Current}";
            LoadData();
            Closing += MainWindow_Closing;
        }

        public void InitializeTray()
        {
            var trayEnabled = IsTrayEnabled();
            TrayIconService.Initialize(this, trayEnabled);
            UpdateCloseButtonHint(trayEnabled);
        }

        public void RefreshTraySettings()
        {
            var trayEnabled = IsTrayEnabled();
            TrayIconService.ApplyTraySetting(trayEnabled);
            UpdateCloseButtonHint(trayEnabled);
        }

        private bool IsTrayEnabled() => _dataManager.Data.Config.TrayIcon;

        private void UpdateCloseButtonHint(bool trayEnabled)
        {
            ToolTip.SetTip(CloseWindowButton, trayEnabled ? "关闭到托盘" : "退出程序");
        }

        private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            if (TrayIconService.IsExiting)
                return;

            if (IsTrayEnabled())
            {
                e.Cancel = true;
                TrayIconService.HideMainWindow();
            }
        }

        private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        }

        private void CloseWindowButton_Click(object? sender, RoutedEventArgs e)
        {
            if (IsTrayEnabled())
            {
                TrayIconService.EnsureTrayIcon();
                Hide();
            }
            else
            {
                TrayIconService.ExitApplication();
            }
        }

        /// <summary>
        /// 加载数据
        /// </summary>
        private void LoadData()
        {
            try
            {
                // 显示应用程序
                ShowApplications();
            }
            catch (Exception ex)
            {
                _ = AppHelper.ShowErrorAsync($"加载数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置活动按钮样式
        /// </summary>
        private void SetActiveButton(Button button)
        {
            // 重置之前的活动按钮
            if (_currentActiveButton != null)
            {
                _currentActiveButton.Classes.Remove("ActiveNavigation");
                _currentActiveButton.Classes.Add("Navigation");
            }

            // 设置新的活动按钮
            _currentActiveButton = button;
            if (_currentActiveButton != null)
            {
                _currentActiveButton.Classes.Remove("Navigation");
                _currentActiveButton.Classes.Add("ActiveNavigation");
            }
        }

        /// <summary>
        /// 显示应用程序
        /// </summary>
        private void ShowApplications()
        {
            var view = new ApplicationsView(_dataManager);
            ContentArea.Content = view;
            DefaultContent.IsVisible = false;
        }

        /// <summary>
        /// 打开设置
        /// </summary>
        private void OpenSettings()
        {
            var settingsWindow = new SettingsWindow(_dataManager);
            settingsWindow.ShowDialog(this);
        }

        #region 事件处理 - Simplified without navigation

        private void SettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            OpenSettings();
        }

        private void GetStartedButton_Click(object? sender, RoutedEventArgs e)
        {
            ShowApplications();
        }

        private void ViewDocumentationButton_Click(object? sender, RoutedEventArgs e)
        {
            AppHelper.OpenInBrowser(VersionInfo.DocumentationUrl);
        }

        private void DevelopmentCard_Click(object? sender, PointerPressedEventArgs e)
        {
            ShowApplications();
        }

        private void SystemCard_Click(object? sender, PointerPressedEventArgs e)
        {
            ShowApplications();
        }

        private void ApplicationCard_Click(object? sender, PointerPressedEventArgs e)
        {
            ShowApplications();
        }

        #endregion

        #region 菜单事件处理

        /// <summary>
        /// 设置菜单项点击
        /// </summary>
        private void SettingsMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            OpenSettings();
        }

        /// <summary>
        /// 退出菜单项点击
        /// </summary>
        private void ExitMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            TrayIconService.ExitApplication();
        }

        /// <summary>
        /// 查看应用程序菜单项点击
        /// </summary>
        private void ViewApplicationsMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            ShowApplications();
        }

        /// <summary>
        /// 系统工具菜单项点击
        /// </summary>
        private void ToolMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string toolKey)
            {
                try
                {
                    WindowsToolsHelper.ExecuteWindowsTool(toolKey);
                    StatusText.Text = $"正在启动 {menuItem.Header}...";
                }
                catch (Exception ex)
                {
                    _ = AppHelper.ShowErrorAsync($"启动工具失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 关于菜单项点击
        /// </summary>
        private void AboutMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            _ = AppHelper.ShowInfoAsync(
                $"ProgramBox {VersionInfo.Display}\n" +
                "编程环境管理工具\n\n" +
                "专为程序员设计的多语言开发环境管理工具\n" +
                "支持 Java、Node.js、Python、C++ 等多种开发环境");
        }

        #endregion
    }
}