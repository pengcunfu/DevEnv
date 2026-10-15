using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using ProgramBox.Utils;
using Velopack;

namespace ProgramBox.Views
{
    public partial class SettingsView : UserControl
    {
        private readonly JsonDataManager _dataManager;
        private readonly UpdateService _updateService = new();
        private bool _isCapturingHotkey;
        private bool _isCheckingOrDownloading;
        private UpdateInfo? _updateInfo;

        public SettingsView()
            : this(new JsonDataManager())
        {
        }

        public SettingsView(JsonDataManager dataManager)
        {
            InitializeComponent();
            _dataManager = dataManager;
            LoadSettings();
            AboutVersionText.Text = $"版本 {VersionInfo.Display}";
            DocUrlText.Text = VersionInfo.DocumentationUrl;
            HotkeyTextBox.KeyDown += HotkeyTextBox_KeyDown;

            // 非 Velopack 安装态（调试 / 旧版 zip）下 CurrentVersion 为 null，回退程序集版本显示
            var installedVersion = _updateService.IsUpdateCapable
                ? _updateService.CurrentVersion?.ToNormalizedString()
                : null;
            UpdateCurrentVersionText.Text = installedVersion is null ? VersionInfo.Display : $"v{installedVersion}";
            if (!_updateService.IsUpdateCapable)
            {
                UpdateStatusText.Text = "当前为非安装版（调试运行或旧版 zip 安装），无法自动更新。请下载安装包安装后，即可通过应用内检查更新升级。";
                GotoReleasesButton.IsVisible = true;
            }
        }

        /// <summary>
        /// 加载设置
        /// </summary>
        private void LoadSettings()
        {
            var config = _dataManager.Data.Config;
            
            TrayIconCheckBox.IsChecked = config.TrayIcon;
            SelfStartingCheckBox.IsChecked = config.SelfStarting;
            EdgeAutoHideCheckBox.IsChecked = config.EdgeAutoHide;
            DarkModeCheckBox.IsChecked = config.DarkMode;
            BaseEnvPathTextBox.Text = config.BaseEnvPath;
            HotkeyTextBox.Text = config.ShowWindowHotkey;

            var launchTag = config.DoubleClickToLaunch ? "Double" : "Single";
            LaunchModeComboBox.SelectedItem = LaunchModeComboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag?.ToString() == launchTag)
                ?? LaunchModeComboBox.Items[0];
            
            // 设置语言选择
            var languageItem = LanguageComboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag?.ToString() == config.Language);
            if (languageItem != null)
            {
                LanguageComboBox.SelectedItem = languageItem;
            }
            else
            {
                LanguageComboBox.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// 保存设置
        /// </summary>
        private void SaveSettings()
        {
            var config = _dataManager.Data.Config;
            
            config.TrayIcon = TrayIconCheckBox.IsChecked ?? true;
            config.SelfStarting = SelfStartingCheckBox.IsChecked ?? false;
            config.EdgeAutoHide = EdgeAutoHideCheckBox.IsChecked ?? false;
            config.DarkMode = DarkModeCheckBox.IsChecked ?? false;
            config.BaseEnvPath = BaseEnvPathTextBox.Text ?? @"D:\ProgramBox\env";
            config.ShowWindowHotkey = HotkeyTextBox.Text?.Trim() ?? string.Empty;
            config.DoubleClickToLaunch = LaunchModeComboBox.SelectedItem is ComboBoxItem launchItem
                && launchItem.Tag?.ToString() == "Double";

            // 立即应用深色/浅色主题
            if (Application.Current != null)
            {
                Application.Current.RequestedThemeVariant = config.DarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
            }
            
            if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                config.Language = selectedItem.Tag?.ToString() ?? "zh-CN";
            }
            
            // 设置开机自启动
            AppHelper.SetAutoStart(config.SelfStarting);
            HotkeyService.ApplyHotkey(config.ShowWindowHotkey);

            TrayIconService.ApplyTraySetting(config.TrayIcon);

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow is MainWindow mainWindow)
            {
                mainWindow.RefreshTraySettings();
                mainWindow.RefreshEdgeAutoHideSettings();
            }
            
            _dataManager.Save();
        }

        #region 事件处理

        private async void BrowsePathButton_Click(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                return;
            }

            var folder = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择环境基础路径",
                AllowMultiple = false
            });

            if (folder.Count > 0)
            {
                BaseEnvPathTextBox.Text = folder[0].Path.LocalPath;
            }
        }

        private void CaptureHotkeyButton_Click(object? sender, RoutedEventArgs e)
        {
            _isCapturingHotkey = true;
            CaptureHotkeyButton.IsEnabled = false;
            HotkeyHintText.IsVisible = true;
            HotkeyTextBox.Focus();

            // 录制期间暂停全局快捷键，避免按下旧组合时窗口弹起打断录制
            HotkeyService.ApplyHotkey(null);
        }

        private void ClearHotkeyButton_Click(object? sender, RoutedEventArgs e)
        {
            CancelHotkeyCapture();
            HotkeyTextBox.Text = string.Empty;
        }

        private void HotkeyTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (!_isCapturingHotkey)
                return;

            e.Handled = true;

            if (e.Key == Key.Escape)
            {
                CancelHotkeyCapture();
                return;
            }

            // 仅按下修饰键时等待主键
            if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            {
                return;
            }

            // 至少需要一个修饰键或功能键，避免注册裸字母键
            var isFunctionKey = e.Key >= Key.F1 && e.Key <= Key.F24;
            if (e.KeyModifiers == KeyModifiers.None && !isFunctionKey)
                return;

            HotkeyTextBox.Text = HotkeyService.FormatHotkey(e.KeyModifiers, e.Key);
            CancelHotkeyCapture();
        }

        private void CancelHotkeyCapture()
        {
            if (!_isCapturingHotkey)
                return;

            _isCapturingHotkey = false;
            CaptureHotkeyButton.IsEnabled = true;
            HotkeyHintText.IsVisible = false;

            // 恢复保存前的快捷键
            HotkeyService.ApplyHotkey(_dataManager.Data.Config.ShowWindowHotkey);
        }

        private async void SaveButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                SaveSettings();
                await AppHelper.ShowInfoAsync("设置已保存");
            }
            catch (Exception ex)
            {
                await AppHelper.ShowErrorAsync($"保存设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 查看文档
        /// </summary>
        private void ViewDocumentationButton_Click(object? sender, RoutedEventArgs e)
        {
            AppHelper.OpenInBrowser(VersionInfo.DocumentationUrl);
        }

        /// <summary>
        /// 检查更新
        /// </summary>
        private async void CheckUpdateButton_Click(object? sender, RoutedEventArgs e)
        {
            if (!_updateService.IsUpdateCapable || _isCheckingOrDownloading)
                return;

            _isCheckingOrDownloading = true;
            CheckUpdateButton.IsEnabled = false;
            UpdateStatusText.Text = "正在检查更新…";
            HideUpdateActions();
            try
            {
                var info = await _updateService.CheckForUpdatesAsync();
                if (info == null)
                {
                    UpdateStatusText.Text = $"已是最新版本 {UpdateCurrentVersionText.Text}";
                }
                else
                {
                    _updateInfo = info;
                    var current = _updateService.CurrentVersion?.ToNormalizedString();
                    UpdateStatusText.Text = current is null
                        ? $"发现新版本 {info.TargetFullRelease.Version}"
                        : $"发现新版本 {info.TargetFullRelease.Version}（当前 {current}）";
                    DownloadUpdateButton.IsVisible = true;
                }
            }
            catch (Exception ex)
            {
                UpdateStatusText.Text = "检查更新失败";
                await AppHelper.ShowErrorAsync($"检查更新失败: {ex.Message}");
            }
            finally
            {
                _isCheckingOrDownloading = false;
                CheckUpdateButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// 下载并更新（优先 delta 增量，失败自动回退完整包）
        /// </summary>
        private async void DownloadUpdateButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_updateInfo is null || _isCheckingOrDownloading)
                return;

            _isCheckingOrDownloading = true;
            CheckUpdateButton.IsEnabled = false;
            DownloadUpdateButton.IsEnabled = false;
            UpdateStatusText.Text = "正在下载更新…";
            UpdateProgressBar.IsVisible = true;
            var progress = new Progress<int>(p => UpdateProgressBar.Value = p);
            try
            {
                var info = await _updateService.DownloadUpdatesAsync(_updateInfo, progress);
                _updateInfo = info;
                UpdateStatusText.Text = "下载完成，重启以应用更新";
                RestartUpdateButton.IsVisible = true;
            }
            catch (Exception ex)
            {
                UpdateStatusText.Text = "下载更新失败";
                await AppHelper.ShowErrorAsync($"下载更新失败: {ex.Message}");
            }
            finally
            {
                _isCheckingOrDownloading = false;
                UpdateProgressBar.IsVisible = false;
                DownloadUpdateButton.IsEnabled = true;
                CheckUpdateButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// 立即退出应用并应用已下载的更新，然后自动重启
        /// </summary>
        private void RestartUpdateButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_updateInfo is null)
                return;

            _updateService.RestartAndApply(_updateInfo);
        }

        /// <summary>
        /// 前往 Releases 下载安装包（非安装版提示用）
        /// </summary>
        private void GotoReleasesButton_Click(object? sender, RoutedEventArgs e)
        {
            AppHelper.OpenInBrowser(VersionInfo.ReleasesUrl);
        }

        /// <summary>
        /// 隐藏更新操作按钮
        /// </summary>
        private void HideUpdateActions()
        {
            DownloadUpdateButton.IsVisible = false;
            RestartUpdateButton.IsVisible = false;
            GotoReleasesButton.IsVisible = false;
        }

        #endregion
    }
}
