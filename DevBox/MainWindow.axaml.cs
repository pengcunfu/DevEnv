using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Threading;
using LavaTranslator.Infrastructure;
using ProgramBox.Utils;
using ProgramBox.Views;

namespace ProgramBox
{
    public partial class MainWindow : Window
    {
        private readonly JsonDataManager _dataManager;
        private readonly DispatcherTimer _statusTimer;
        private ApplicationsView? _applicationsView;
        private IGlobalHotkey? _translatorHotkey;

        public MainWindow()
        {
            InitializeComponent();
            TitleBarIcon.Source = AppIconHelper.LoadTitleIcon();
            _dataManager = new JsonDataManager();
            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _statusTimer.Tick += (_, _) => ResetStatusText();
            VersionText.Text = $"版本 {VersionInfo.Current}";
            LoadData();
            SyncAutoStartSetting();
            Closing += MainWindow_Closing;
            Closed += (_, _) =>
            {
                _translatorHotkey?.Dispose();
                _translatorHotkey = null;
                _applicationsView?.Clipboard?.DisposeHotkey();
            };

            HotkeyService.HotkeyPressed += OnHotkeyPressed;
        }

        /// <summary>
        /// 全局快捷键：显示并居中主窗口
        /// </summary>
        private void OnHotkeyPressed()
        {
            ShowCentered();
        }

        public void ShowCentered()
        {
            Show();
            WindowState = WindowState.Normal;
            CenterOnScreen();
            Activate();
        }

        private void CenterOnScreen()
        {
            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            if (screen == null)
                return;

            var area = screen.WorkingArea;
            var scale = screen.Scaling;
            var width = Math.Max(1, (int)Math.Round(ClientSize.Width * scale));
            var height = Math.Max(1, (int)Math.Round(ClientSize.Height * scale));

            Position = new PixelPoint(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2);
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            // 无边框窗口：挂接原生 WM_NCHITTEST，由系统接管边缘缩放与缩放光标
            var hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            WindowResizeHelper.Attach(hwnd);
            HotkeyService.Attach(hwnd);
            HotkeyService.ApplyHotkey(_dataManager.Data.Config.ShowWindowHotkey);
            EnsureTranslatorHotkey();
            _applicationsView?.Clipboard?.EnsureHotkey();
        }

        /// <summary>
        /// 注册熔岩翻译 Alt+Space（与 DevBox 显示窗口热键并存）
        /// </summary>
        private void EnsureTranslatorHotkey()
        {
            if (_translatorHotkey != null || _applicationsView?.Translator == null)
                return;

            _translatorHotkey = GlobalHotkey.Create(this);
            _translatorHotkey.HotkeyPressed += (_, _) =>
                _applicationsView.Translator.HandleHotkeyPressed();
            _translatorHotkey.RegisterAltSpace();
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
            _applicationsView = view;
            ContentArea.Content = view;
            DefaultContent.IsVisible = false;
            EnsureTranslatorHotkey();
            view.Clipboard?.EnsureHotkey();
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
