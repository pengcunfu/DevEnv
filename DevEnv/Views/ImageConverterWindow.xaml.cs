using DevEnv.UI;
using Avalonia.Controls;
using DevEnv.ViewModels;

namespace DevEnv.Views;

public partial class ImageConverterWindow : Window
{
    private readonly ImageConverterViewModel _viewModel;

    public ImageConverterWindow()
    {
        InitializeComponent();
        _viewModel = new ImageConverterViewModel();
        DataContext = _viewModel;
        OutputFormatComboBox.SelectionChanged += OutputFormatComboBox_SelectionChanged;
    }

    private void OutputFormatComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            var format = selectedItem.Content?.ToString();
            if (!string.IsNullOrEmpty(format))
                _viewModel.UpdateOutputFormat(format);
        }
    }

    private async void SelectFilesButton_Click(object? sender, RoutedEventArgs e)
    {
        var files = await FileDialogs.OpenFilesAsync(
            this,
            "选择要转换的图像文件",
            allowMultiple: true,
            filterName: "图像文件",
            extensions: ["jpg", "jpeg", "png", "bmp", "gif", "tiff", "tif", "webp", "ico", "jfif", "avif", "heic", "heif", "svg", "*"]);

        if (files is { Length: > 0 })
            _viewModel.AddFiles(files);
    }

    private async void SelectFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync(this, "选择要转换的图像文件所在的文件夹");
        if (!string.IsNullOrEmpty(folder))
            _viewModel.AddFolder(folder);
    }

    private async void BrowseOutputButton_Click(object? sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync(this, "选择转换后图像的保存目录");
        if (!string.IsNullOrEmpty(folder))
            _viewModel.OutputDirectory = folder;
    }

    private async void StartConversionButton_Click(object? sender, RoutedEventArgs e)
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

    private void ClearLogButton_Click(object? sender, RoutedEventArgs e) => _viewModel.ClearLog();
}
