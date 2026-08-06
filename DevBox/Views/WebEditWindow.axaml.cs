using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using ProgramBox.Models;
using ProgramBox.Utils;

namespace ProgramBox.Views
{
    public partial class WebEditWindow : Window
    {
        private readonly WebAtom _web;
        private string? _pendingIconPath;

        public bool Saved { get; private set; }

        public WebEditWindow(WebAtom web)
        {
            InitializeComponent();
            _web = web;
            TitleTextBox.Text = web.Name;
            UrlTextBox.Text = web.Url;
            RefreshIconPreview(web.IconPath);
        }

        private void RefreshIconPreview(string? iconPath)
        {
            if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
                IconPreview.Source = new Bitmap(iconPath);
            else
                IconPreview.Source = null;
        }

        private async void RefreshIconButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                RefreshIconButton.IsEnabled = false;
                _pendingIconPath = await WebShortcutHelper.RefreshFaviconAsync(_web);
                RefreshIconPreview(_pendingIconPath);
            }
            catch (Exception ex)
            {
                _ = AppHelper.ShowErrorAsync($"获取图标失败: {ex.Message}");
            }
            finally
            {
                RefreshIconButton.IsEnabled = true;
            }
        }

        private async void PickIconButton_Click(object? sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择图标图片",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("图片") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.ico", "*.bmp"] }
                ]
            });

            if (files.Count == 0)
                return;

            _pendingIconPath = files[0].Path.LocalPath;
            RefreshIconPreview(_pendingIconPath);
        }

        private void SaveButton_Click(object? sender, RoutedEventArgs e)
        {
            var title = TitleTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(title))
            {
                _ = AppHelper.ShowErrorAsync("请填写显示标题。");
                return;
            }

            _web.Name = title;

            if (!string.IsNullOrEmpty(_pendingIconPath))
            {
                var iconsRoot = Path.GetFullPath(IconCacheHelper.IconsDirectory);
                var pendingFull = Path.GetFullPath(_pendingIconPath);
                var newIcon = pendingFull.StartsWith(iconsRoot, StringComparison.OrdinalIgnoreCase)
                    ? pendingFull
                    : WebShortcutHelper.SetCustomIcon(_web, _pendingIconPath) ?? _web.IconPath;

                if (!string.Equals(_web.IconPath, newIcon, StringComparison.OrdinalIgnoreCase))
                {
                    IconCacheHelper.TryDeleteCachedIcon(_web.IconPath);
                    _web.IconPath = newIcon;
                }
            }

            Saved = true;
            Close();
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            Saved = false;
            Close();
        }
    }
}
