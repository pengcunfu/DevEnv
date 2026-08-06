using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ProgramBox;

namespace ProgramBox.Utils
{
    /// <summary>
    /// 系统托盘：关闭窗口时隐藏到托盘，从托盘恢复或退出。
    /// </summary>
    public static class TrayIconService
    {
        private static TrayIcon? _trayIcon;
        private static MainWindow? _mainWindow;
        private static bool _isExiting;

        public static bool IsExiting => _isExiting;

        /// <summary>
        /// 启动时调用：绑定主窗口、设置图标、创建托盘。
        /// </summary>
        public static void Initialize(MainWindow mainWindow, bool showTray)
        {
            _mainWindow = mainWindow;
            _isExiting = false;

            ApplyWindowIcon(mainWindow);
            EnsureShutdownMode();

            DisposeTray();
            if (showTray)
                CreateTrayIcon();
        }

        public static void ApplyTraySetting(bool enabled)
        {
            if (_mainWindow != null)
                ApplyWindowIcon(_mainWindow);

            EnsureShutdownMode();

            if (enabled)
            {
                if (_trayIcon == null)
                    CreateTrayIcon();
                else
                {
                    _trayIcon.IsVisible = true;
                    RefreshTrayIconImage();
                }
            }
            else
            {
                DisposeTray();
            }
        }

        public static void EnsureTrayIcon()
        {
            EnsureShutdownMode();

            if (_trayIcon == null)
                CreateTrayIcon();
            else
                RefreshTrayIconImage();
        }

        public static void HideMainWindow()
        {
            if (_mainWindow == null)
                return;

            _mainWindow.Hide();
        }

        public static void ShowMainWindow()
        {
            if (_mainWindow == null)
                return;

            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        }

        public static void RequestClose(bool trayEnabled)
        {
            if (trayEnabled)
            {
                EnsureTrayIcon();
                HideMainWindow();
                return;
            }

            ExitApplication();
        }

        public static void ExitApplication()
        {
            _isExiting = true;
            DisposeTray();

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
        }

        private static void ApplyWindowIcon(Window window)
        {
            var icon = AppIconHelper.Load();
            if (icon != null)
                window.Icon = icon;
        }

        private static void EnsureShutdownMode()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        private static void CreateTrayIcon()
        {
            if (_trayIcon != null)
                return;

            var icon = AppIconHelper.LoadForTray();
            if (icon == null)
                return;

            _trayIcon = new TrayIcon
            {
                Icon = icon,
                ToolTipText = "DevBox",
                IsVisible = true
            };

            _trayIcon.Clicked += OnTrayClicked;

            var menu = new NativeMenu();
            var showItem = new NativeMenuItem("显示主窗口");
            showItem.Click += (_, _) => ShowMainWindow();
            menu.Items.Add(showItem);
            menu.Items.Add(new NativeMenuItemSeparator());
            var exitItem = new NativeMenuItem("退出");
            exitItem.Click += (_, _) => ExitApplication();
            menu.Items.Add(exitItem);
            _trayIcon.Menu = menu;
        }

        private static void RefreshTrayIconImage()
        {
            if (_trayIcon == null)
                return;

            var icon = AppIconHelper.LoadForTray();
            if (icon != null)
                _trayIcon.Icon = icon;
        }

        private static void OnTrayClicked(object? sender, EventArgs e) => ShowMainWindow();

        private static void DisposeTray()
        {
            if (_trayIcon == null)
                return;

            _trayIcon.Clicked -= OnTrayClicked;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
    }
}
