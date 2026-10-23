using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBox.Core.Services;

namespace DevBox.Host.ViewModels;

/// <summary>JSON 格式化器：格式化 / 压缩。</summary>
public sealed partial class JsonToolViewModel : ObservableObject
{
    public JsonToolViewModel()
    {
        Text = "{\n  \"hello\": \"world\"\n}";
    }

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [RelayCommand]
    private void Format() => Apply(minify: false);

    [RelayCommand]
    private void Minify() => Apply(minify: true);

    private void Apply(bool minify)
    {
        var result = CoreServices.Utility.FormatJson(Text, minify);
        if (result.Ok)
        {
            Text = result.Message ?? Text;
            Message = minify ? "已压缩" : "已格式化";
        }
        else
        {
            Message = result.DisplayMessage;
        }
    }
}