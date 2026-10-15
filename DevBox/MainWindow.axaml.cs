using System;
using System.Linq;
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
        private enum DockEdge
        {
            None,
            Left,
            Right,
            Top
        }

        private const int EdgeDockThreshold = 8;
        private const int RetractStripSize = 8;

        private readonly JsonDataManager _dataManager;
        private readonly DispatcherTimer _statusTimer;
        private ApplicationsView? _applicationsView;
        private IGlobalHotkey? _translatorHotkey;

        private bool _edgeAutoHideEnabled;
        private bool _suppressPositionEvents;
        private bool _retracted;
        private DockEdge _dockedEdge = DockEdge.None;

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
            };

            PositionChanged += (_, _) => OnWindowPositionChanged();
            PointerEntered += MainWindow_PointerEntered;
            PointerExited += MainWindow_PointerExited;
            RefreshEdgeAutoHideSettings();

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
            // 解除靠边状态，避免恢复后立刻又贴边或收缩
            _dockedEdge = DockEdge.None;
            _retracted = false;
            WindowResizeHelper.SuppressEdgeHitTest = false;
            Topmost = false;

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
        /// 根据设置启用或禁用靠边悬停隐藏
        /// </summary>
        public void RefreshEdgeAutoHideSettings()
        {
            _edgeAutoHideEnabled = _dataManager.Data.Config.EdgeAutoHide;
            if (_edgeAutoHideEnabled)
            {
                OnWindowPositionChanged();
            }
            else
            {
                RestoreFromEdge();
            }
        }

        private void OnWindowPositionChanged()
        {
            if (_suppressPositionEvents || !_edgeAutoHideEnabled || WindowState == WindowState.Maximized)
                return;

            var screen = Screens.ScreenFromWindow(this);
            if (screen == null)
                return;

            var winRect = GetWindowRectOnScreen(screen);
            var edge = GetDockEdge(winRect, screen.WorkingArea);
            if (edge == DockEdge.None)
            {
                _dockedEdge = DockEdge.None;
                _retracted = false;
                return;
            }

            // 收缩状态下保持收缩，等待鼠标悬停边缘条后恢复
            if (_retracted)
                return;

            if (_dockedEdge != edge)
                _dockedEdge = edge;
        }

        private PixelRect GetWindowRectOnScreen(Screen screen)
        {
            var width = Math.Max(1, (int)Math.Round(ClientSize.Width * screen.Scaling));
            var height = Math.Max(1, (int)Math.Round(ClientSize.Height * screen.Scaling));
            return new PixelRect(Position, new PixelSize(width, height));
        }

        private static DockEdge GetDockEdge(PixelRect winRect, PixelRect area)
        {
            if (winRect.X <= area.X + EdgeDockThreshold)
                return DockEdge.Left;
            if (area.X + area.Width - (winRect.X + winRect.Width) <= EdgeDockThreshold)
                return DockEdge.Right;
            if (winRect.Y <= area.Y + EdgeDockThreshold)
                return DockEdge.Top;

            return DockEdge.None;
        }

        private static PixelPoint ComputeDockedPosition(DockEdge edge, PixelRect winRect, PixelRect area)
        {
            return edge switch
            {
                DockEdge.Left => new PixelPoint(area.X, winRect.Y),
                DockEdge.Right => new PixelPoint(area.X + area.Width - winRect.Width, winRect.Y),
                _ => new PixelPoint(winRect.X, area.Y),
            };
        }

        private void MainWindow_PointerEntered(object? sender, PointerEventArgs e)
        {
            if (_edgeAutoHideEnabled)
                RestoreFromEdge();
        }

        private void MainWindow_PointerExited(object? sender, PointerEventArgs e)
        {
            if (!_edgeAutoHideEnabled || _dockedEdge == DockEdge.None)
                return;

            if (OwnedWindows.Any(w => w.IsVisible))
                return;

            RetractToEdge();
        }

        private void RetractToEdge()
        {
            if (_dockedEdge == DockEdge.None || _retracted || WindowState == WindowState.Maximized)
                return;

            var screen = Screens.ScreenFromWindow(this);
            if (screen == null)
                return;

            var winRect = GetWindowRectOnScreen(screen);
            var area = screen.WorkingArea;

            var target = _dockedEdge switch
            {
                DockEdge.Left => new PixelPoint(area.X - winRect.Width + RetractStripSize, winRect.Y),
                DockEdge.Right => new PixelPoint(area.X + area.Width - RetractStripSize, winRect.Y),
                _ => new PixelPoint(winRect.X, area.Y - winRect.Height + RetractStripSize),
            };

            _retracted = true;
            WindowResizeHelper.SuppressEdgeHitTest = true;
            // 收缩后置顶显示边缘条，避免被其他窗口遮挡导致悬停无效
            Topmost = true;
            MoveWindowTo(target);
        }

        private void RestoreFromEdge()
        {
            if (_dockedEdge == DockEdge.None || !_retracted)
                return;

            var screen = Screens.ScreenFromWindow(this);
            if (screen == null)
            {
                _retracted = false;
                return;
            }

            var target = ComputeDockedPosition(_dockedEdge, GetWindowRectOnScreen(screen), screen.WorkingArea);
            _retracted = false;
            WindowResizeHelper.SuppressEdgeHitTest = false;
            Topmost = false;
            MoveWindowTo(target);
        }

        private void MoveWindowTo(PixelPoint target)
        {
            _suppressPositionEvents = true;
            try
            {
                Position = target;
            }
            finally
            {
                _suppressPositionEvents = false;
            }
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
