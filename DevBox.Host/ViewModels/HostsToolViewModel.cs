using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBox.Core.Services;

namespace DevBox.Host.ViewModels;

/// <summary>Hosts 文件编辑器：非管理员只读，可拉起管理员窗口另存。</summary>
public sealed partial class HostsToolViewModel : ObservableObject
{
    public HostsToolViewModel() => Load();

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private string _path = string.Empty;

    [ObservableProperty]
    private bool _canWrite;

    [ObservableProperty]
    private string _message = string.Empty;

    [RelayCommand]
    public void Load()
    {
        var view = CoreServices.Utility.ReadHosts();
        Path = view.Path;
        Text = view.Text;
        CanWrite = view.CanWrite;
        Message = CanWrite ? "可写" : "只读（需管理员）";
    }

    [RelayCommand]
    private void Save()
    {
        var result = CoreServices.Utility.SaveHosts(Text);
        if (result.Ok)
        {
            CanWrite = true;
            Message = "已保存";
        }
        else
        {
            Message = result.DisplayMessage;
        }
    }

    [RelayCommand]
    private void Elevate()
    {
        var result = CoreServices.Utility.ElevateHosts();
        Message = result.Ok ? "已请求管理员权限打开 Hosts 编辑" : result.DisplayMessage;
    }
}