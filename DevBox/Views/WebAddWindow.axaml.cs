using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using ProgramBox.Utils;

namespace ProgramBox.Views
{
    public partial class WebAddWindow : Window
    {
        private WebMetadata? _metadata;
        private string? _pendingIconPath;

        public WebMetadata? Result { get; private set; }

        public WebAddWindow()
        {
            InitializeComponent();
        }

        public WebAddWindow(string initialUrl) : this()
        {
            UrlTextBox.Text = initialUrl;
        }

        private async void FetchButton_Click(object? sender, RoutedEventArgs e)
        {
            await FetchAsync();
        }

        private async System.Threading.Tasks.Task FetchAsync()
        {
            try
            {
                FetchButton.IsEnabled = false;
                StatusText.Text = "正在获取网站信息…";

                _metadata = await WebShortcutHelper.FetchMetadataAsync(UrlTextBox.Text ?? string.Empty);

                TitleTextBox.Text = _metadata.Title;
                UrlPreview.Text = _metadata.Url;
                _pendingIconPath = _metadata.IconPath;
                PreviewPanel.IsVisible = true;
                AddButton.IsEnabled = true;
                StatusText.Text = "获取成功，可修改标题与图标后再添加。";

                if (!string.IsNullOrEmpty(_metadata.IconPath) && File.Exists(_metadata.IconPath))
                    IconPreview.Source = new Bitmap(_metadata.IconPath);
                else
                    IconPreview.Source = null;
            }
            catch (Exception ex)
            {
                _metadata = null;
                PreviewPanel.IsVisible = false;
                AddButton.IsEnabled = false;
                StatusText.Text = $"获取失败: {ex.Message}";
            }
            finally
            {
                FetchButton.IsEnabled = true;
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
            if (File.Exists(_pendingIconPath))
                IconPreview.Source = new Bitmap(_pendingIconPath);
        }

        private async void AddButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_metadata == null)
            {
                await FetchAsync();
                if (_metadata == null)
                    return;
            }

            var title = TitleTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(title))
            {
                await AppHelper.ShowErrorAsync("请填写显示标题。");
                return;
            }

            var iconPath = _pendingIconPath ?? _metadata.IconPath;
            if (!string.IsNullOrEmpty(_pendingIconPath)
                && File.Exists(_pendingIconPath)
                && !string.Equals(_pendingIconPath, _metadata.IconPath, StringComparison.OrdinalIgnoreCase))
            {
                var cacheKey = $"web_{IconCacheHelper.ComputeHash(_metadata.Url)}_custom";
                iconPath = IconCacheHelper.CopyImageToCache(_pendingIconPath, cacheKey) ?? iconPath;
            }

            Result = new WebMetadata(_metadata.Url, title, iconPath ?? string.Empty);
            Close();
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            Result = null;
            Close();
        }
    }
}
