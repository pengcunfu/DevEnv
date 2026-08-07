using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Threading;
using ProgramBox.Utils;
using ProgramBox.Views;

namespace ProgramBox
{
    public partial class MainWindow : Window
    {
        private readonly JsonDataManager _dataManager;
        private readonly DispatcherTimer _statusTimer;

        public MainWindow()
        {
            InitializeComponent();
            _dataManager = new JsonDataManager();
            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _statusTimer.Tick += (_, _) => ResetStatusText();
            VersionText.Text = $"版本 {VersionInfo.Current}";
            LoadData();
            SyncAutoStartSetting();
            Closing += MainWindow_Closing;
        }

        /// <summary>
        /// 在底部状态条显示消息，数秒后恢复默认文本
        /// </summary>
        public void ShowStatusMessage(string message)
        {
            StatusText.Text = message;
            _statusTimer.Stop();
            _statusTimer.Start();
        }

        private void ResetStatusText()
        {
            StatusText.Text = "就绪";
            _statusTimer.Stop();
        }

        /// <summary>
        /// 启动时同步开机自启动状态，保证配置与注册表一致
        /// </summary>
        private void SyncAutoStartSetting()
        {
            var config = _dataManager.Data.Config;
            var effective = AppHelper.SyncAutoStart(config.SelfStarting);
            if (effective != config.SelfStarting)
            {
                config.SelfStarting = effective;
                _dataManager.Save();
            }
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

        private void ResizeWest(object? sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.West, e);

        private void ResizeEast(object? sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.East, e);

        private void ResizeNorth(object? sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.North, e);

        private void ResizeSouth(object? sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.South, e);

        private void ResizeNorthWest(object? sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.NorthWest, e);

        private void ResizeNorthEast(object? sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.NorthEast, e);

        private void ResizeSouthWest(object? sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.SouthWest, e);

        private void ResizeSouthEast(object? sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.SouthEast, e);

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
        /// 显示应用程序
        /// </summary>
        private void ShowApplications()
        {
            var view = new ApplicationsView(_dataManager);
            ContentArea.Content = view;
            DefaultContent.IsVisible = false;
        }

        #region 事件处理 - Simplified without navigation

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

    }
}
