using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core;
using DevEnv.Core.Services;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.ViewModels;

/// <summary>文件哈希计算器：文件 / 文本两种模式。</summary>
public sealed partial class HashToolViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isFileMode = true;

    [ObservableProperty]
    private string _path = string.Empty;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private string _algorithm = "SHA256";

    [ObservableProperty]
    private string _result = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    public IReadOnlyList<string> Algorithms { get; } = ["MD5", "SHA1", "SHA256", "SHA384", "SHA512"];

    [RelayCommand]
    private void PickFile()
    {
        var files = HostCallbacks.PickFiles?.Invoke("所有文件|*.*");
        if (files is { Length: > 0 })
            Path = files[0];
    }

    [RelayCommand]
    private async Task CalculateAsync()
    {
        var (result, hash) = await CoreServices.Utility.CalculateHashAsync(
            Algorithm,
            IsFileMode ? Path : null,
            IsFileMode ? null : Text);

        if (!result.Ok)
        {
            Result = string.Empty;
            Message = result.DisplayMessage;
            return;
        }

        Result = hash?.Hash ?? string.Empty;
        Message = hash is null
            ? string.Empty
            : $"{hash.Algorithm} · {hash.ElapsedMs:0} ms{(hash.FileSize.Length > 0 ? " · " + hash.FileSize + " 字节" : string.Empty)}";
    }
}