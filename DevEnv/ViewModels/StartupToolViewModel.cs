using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core.Models;
using DevEnv.Core.Services;

namespace DevEnv.Host.ViewModels;

/// <summary>启动项管理：当前用户 Run 注册表 + 启动文件夹。</summary>
public sealed partial class StartupToolViewModel : ObservableObject
{
    public StartupToolViewModel()
    {
        Items = [];
        Reload();
    }

    public ObservableCollection<StartupEntry> Items { get; }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _command = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [RelayCommand]
    public void Reload()
    {
        Items.Clear();
        foreach (var item in CoreServices.Startup.GetEntries())
            Items.Add(item);
    }

    [RelayCommand]
    private void Add()
    {
        var result = CoreServices.Startup.Add(Name, Command);
        if (result.Ok)
        {
            Name = string.Empty;
            Command = string.Empty;
            Message = "已添加";
        }
        else
        {
            Message = result.DisplayMessage;
        }
        Reload();
    }

    [RelayCommand]
    private void Remove(StartupEntry? entry)
    {
        if (entry is null)
            return;
        var result = CoreServices.Startup.Delete(entry.Id);
        if (!result.Ok)
            Message = result.DisplayMessage;
        Reload();
    }
}