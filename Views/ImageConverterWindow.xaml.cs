using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DevEnv.ViewModels;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace DevEnv.Views
{
    public partial class ImageConverterWindow : Window
    {
        private readonly ImageConverterViewModel _viewModel;

        public ImageConverterWindow()
        {
            InitializeComponent();
            _viewModel = new ImageConverterViewModel();
            this.DataContext = _viewModel;

            // 绑定格式变化事件
            OutputFormatComboBox.SelectionChanged += OutputFormatComboBox_SelectionChanged;
        }

        private void OutputFormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string format = selectedItem.Content.ToString();
                _viewModel.UpdateOutputFormat(format);
            }
        }

        private void SelectFilesButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择要转换的图像文件",
                Filter = "图像文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.tif;*.webp;*.ico;*.jfif;*.avif;*.heic;*.heif;*.svg|所有文件|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                _viewModel.AddFiles(dialog.FileNames);
            }
        }

        private void SelectFolderButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new CommonOpenFileDialog())
            {
                dialog.Title = "选择要转换的图像文件所在的文件夹";
                dialog.IsFolderPicker = true;
                dialog.Multiselect = false;
                dialog.EnsurePathExists = true;

                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    _viewModel.AddFolder(dialog.FileName);
                }
            }
        }

        private void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new CommonOpenFileDialog())
            {
                dialog.Title = "选择转换后图像的保存目录";
                dialog.IsFolderPicker = true;
                dialog.Multiselect = false;
                dialog.EnsurePathExists = true;

                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    _viewModel.OutputDirectory = dialog.FileName;
                }
            }
        }

        private async void StartConversionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.CanStartConversion)
            {
                MessageBox.Show("请先选择要转换的文件和输出目录。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StartConversionButton.IsEnabled = false;

            try
            {
                await _viewModel.StartConversionAsync();

                MessageBox.Show(
                    $"转换完成！\n成功: {_viewModel.SuccessCount}\n失败: {_viewModel.ErrorCount}",
                    "转换完成",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"转换过程中发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                StartConversionButton.IsEnabled = true;
            }
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ClearLog();
        }
    }
}