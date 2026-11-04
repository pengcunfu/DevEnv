using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DevEnv.Host.SigXor.ViewModels;

/// <summary>提示信息内容（不支持预览、文件过大、文件夹属性等）。</summary>
public sealed partial class InfoContentViewModel : ObservableObject
{
    public InfoContentViewModel(string title, string message, string? actionLabel = null, Action? action = null)
    {
        Title = title;
        Message = message;
        ActionLabel = actionLabel;
        ActionCommand = action is null ? null : new RelayCommand(action);
    }

    public string Title { get; }

    [ObservableProperty]
    private string _message = "";

    public string? ActionLabel { get; }
    public ICommand? ActionCommand { get; }
}
