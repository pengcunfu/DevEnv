using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ProgramBox.Utils;

namespace ProgramBox.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly JsonDataManager _dataManager;

        public SettingsWindow(JsonDataManager dataManager)
        {
            InitializeComponent();
            _dataManager = dataManager;
            LoadSettings();
        }

        /// <summary>
        /// 加载设置
        /// </summary>
        private void LoadSettings()
        {
            var config = _dataManager.Data.Config;
            
            TrayIconCheckBox.IsChecked = config.TrayIcon;
            SelfStartingCheckBox.IsChecked = config.SelfStarting;
            DarkModeCheckBox.IsChecked = config.DarkMode;
            BaseEnvPathTextBox.Text = config.BaseEnvPath;
            
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
            config.DarkMode = DarkModeCheckBox.IsChecked ?? false;
            config.BaseEnvPath = BaseEnvPathTextBox.Text ?? @"D:\ProgramBox\env";
            
            if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                config.Language = selectedItem.Tag?.ToString() ?? "zh-CN";
            }
            
            // 设置开机自启动
            AppHelper.SetAutoStart(config.SelfStarting);

            TrayIconService.ApplyTraySetting(config.TrayIcon);

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow is MainWindow mainWindow)
            {
                mainWindow.RefreshTraySettings();
            }
            
            _dataManager.Save();
        }

        #region 事件处理

        private async void BrowsePathButton_Click(object? sender, RoutedEventArgs e)
        {
            var folder = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择环境基础路径",
                AllowMultiple = false
            });

            if (folder.Count > 0)
            {
                BaseEnvPathTextBox.Text = folder[0].Path.LocalPath;
            }
        }

        private async void SaveButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                SaveSettings();
                await AppHelper.ShowInfoAsync("设置已保存");
                Close();
            }
            catch (Exception ex)
            {
                await AppHelper.ShowErrorAsync($"保存设置失败: {ex.Message}");
            }
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion
    }
}
