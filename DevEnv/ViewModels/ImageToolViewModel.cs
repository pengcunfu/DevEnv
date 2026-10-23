using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core;
using DevEnv.Core.Models;
using DevEnv.Core.Services;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.ViewModels;

/// <summary>图像格式转换器：批量转换图片格式。</summary>
public sealed partial class ImageToolViewModel : ObservableObject
{
    public ImageToolViewModel()
    {
        Formats = ["png", "jpg", "webp", "bmp", "gif", "tiff", "ico"];
        Files = [];
        Results = [];
    }

    public IReadOnlyList<string> Formats { get; }

    public ObservableCollection<string> Files { get; }

    public ObservableCollection<ImageConvertResult> Results { get; }

    [ObservableProperty]
    private string _format = "png";

    [ObservableProperty]
    private int _quality = 95;

    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [RelayCommand]
    private void PickFiles()
    {
        var files = HostCallbacks.PickFiles?.Invoke(
            "图片|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.tif;*.webp;*.ico|所有文件|*.*");
        if (files is null)
            return;

        Files.Clear();
        foreach (var file in files)
            Files.Add(file);
    }

    [RelayCommand]
    private void PickOutputDirectory()
    {
        var dir = HostCallbacks.PickFolder?.Invoke();
        if (!string.IsNullOrWhiteSpace(dir))
            OutputDirectory = dir;
    }

    [RelayCommand]
    private async Task ConvertAsync()
    {
        if (Files.Count == 0)
        {
            Message = "请先选择图片";
            return;
        }

        var batch = await CoreServices.Utility.ConvertImagesAsync(
            Files, Format, string.IsNullOrWhiteSpace(OutputDirectory) ? null : OutputDirectory, Quality);

        Results.Clear();
        foreach (var item in batch.Results)
            Results.Add(item);

        Message = $"完成：{batch.SuccessCount}/{batch.Results.Count}";
    }
}