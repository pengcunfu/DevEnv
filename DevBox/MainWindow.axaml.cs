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
