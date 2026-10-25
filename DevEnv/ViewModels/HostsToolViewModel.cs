using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core.Services;

namespace DevEnv.Host.ViewModels;

/// <summary>Hosts 文件编辑器：打开入口已确保管理员权限，这里负责读写与状态提示。</summary>
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
}