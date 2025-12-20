using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DevEnv.ViewModels;

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
            // 简化实现：使用一个输入框让用户输入文件夹路径
            var folderDialog = new FolderSelectionDialog();
            folderDialog.Owner = this;
            if (folderDialog.ShowDialog() == true)
            {
                _viewModel.AddFolder(folderDialog.SelectedPath);
            }
        }

        private void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
        {
            // 简化实现：使用一个输入框让用户输入文件夹路径
            var folderDialog = new FolderSelectionDialog();
            folderDialog.Owner = this;
            if (folderDialog.ShowDialog() == true)
            {
                _viewModel.OutputDirectory = folderDialog.SelectedPath;
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

    // 简单的文件夹选择对话框
    public class FolderSelectionDialog : Window
    {
        public string SelectedPath { get; private set; } = string.Empty;

        public FolderSelectionDialog()
        {
            Title = "选择文件夹";
            Width = 500;
            Height = 200;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var grid = new Grid();
            grid.Margin = new Thickness(15);

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new Label { Content = "请输入文件夹路径：" };
            grid.Children.Add(label);
            Grid.SetRow(label, 0);

            var textBox = new TextBox
            {
                Margin = new Thickness(0, 10, 0, 0),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(textBox);
            Grid.SetRow(textBox, 1);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };
            grid.Children.Add(buttonPanel);
            Grid.SetRow(buttonPanel, 2);

            var okButton = new Button { Content = "确定", Width = 80, Height = 30, Margin = new Thickness(0, 0, 10, 0), IsDefault = true };
            var cancelButton = new Button { Content = "取消", Width = 80, Height = 30, IsCancel = true };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            okButton.Click += (s, e) =>
            {
                SelectedPath = textBox.Text.Trim();
                if (string.IsNullOrEmpty(SelectedPath))
                {
                    MessageBox.Show("请输入文件夹路径。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!Directory.Exists(SelectedPath))
                {
                    MessageBox.Show("指定的文件夹不存在。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                DialogResult = true;
                Close();
            };

            cancelButton.Click += (s, e) =>
            {
                DialogResult = false;
                Close();
            };

            Content = grid;

            // 设置默认路径
            textBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }
    }
}