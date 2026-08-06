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
    public partial class FolderEditWindow : Window
    {
        private readonly FolderAtom _folder;
        private string? _pendingIconPath;

        public bool Saved { get; private set; }

        public FolderEditWindow(FolderAtom folder)
        {
            InitializeComponent();
            _folder = folder;
            AliasTextBox.Text = folder.Name;
            PathTextBox.Text = folder.FolderPath;
            RefreshIconPreview(folder.IconPath);
        }

        private void RefreshIconPreview(string? iconPath)
        {
            if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
                IconPreview.Source = new Bitmap(iconPath);
            else
                IconPreview.Source = null;
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

        private void ResetIconButton_Click(object? sender, RoutedEventArgs e)
        {
            _pendingIconPath = FolderShortcutHelper.ExtractFolderIconToCache(_folder.FolderPath) ?? string.Empty;
            RefreshIconPreview(_pendingIconPath);
        }

        private void SaveButton_Click(object? sender, RoutedEventArgs e)
        {
            var alias = AliasTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(alias))
            {
                _ = AppHelper.ShowErrorAsync("请填写显示名称（别名）。");
                return;
            }

            _folder.Name = alias;

            if (!string.IsNullOrEmpty(_pendingIconPath))
            {
                var iconsRoot = Path.GetFullPath(IconCacheHelper.IconsDirectory);
                var pendingFull = Path.GetFullPath(_pendingIconPath);
                var newIcon = pendingFull.StartsWith(iconsRoot, StringComparison.OrdinalIgnoreCase)
                    ? pendingFull
                    : FolderShortcutHelper.SetCustomIcon(_folder, _pendingIconPath) ?? _folder.IconPath;

                if (!string.Equals(_folder.IconPath, newIcon, StringComparison.OrdinalIgnoreCase))
                {
                    IconCacheHelper.TryDeleteCachedIcon(_folder.IconPath);
                    _folder.IconPath = newIcon;
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
