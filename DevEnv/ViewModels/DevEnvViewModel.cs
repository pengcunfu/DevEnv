using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core.Models;
using DevEnv.Core.Services;

namespace DevEnv.Host.ViewModels;

/// <summary>开发环境页：本机 MySQL / Redis / PostgreSQL 进程启停。</summary>
public sealed partial class DevEnvViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _timer;

    public DevEnvViewModel()
    {
        Services = [];
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => Reload();
        _timer.Start();
        Reload();
    }

    public ObservableCollection<DevServiceItem> Services { get; }

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private AppConfig _config = new();

    [ObservableProperty]
    private string? _busyKey;

    [RelayCommand]
    public void Reload()
    {
        Services.Clear();
        foreach (var item in CoreServices.DevEnvServices.GetServices())
            Services.Add(item);
        Config = CoreServices.Settings.GetConfig();
    }

    [RelayCommand]
    private void Start(DevServiceItem? item)
    {
        if (item is null)
            return;
        BusyKey = item.Key;
        try
        {
            var result = CoreServices.DevEnvServices.Start(item.Key);
            Message = result.DisplayMessage;
            Reload();
        }
        finally
        {
            BusyKey = null;
        }
    }

    [RelayCommand]
    private void Stop(DevServiceItem? item)
    {
        if (item is null)
            return;
        BusyKey = item.Key;
        try
        {
            var result = CoreServices.DevEnvServices.Stop(item.Key);
            Message = result.DisplayMessage;
            Reload();
        }
        finally
        {
            BusyKey = null;
        }
    }

    [RelayCommand]
    private void OpenData(DevServiceItem? item)
    {
        if (item is null)
            return;
        var result = CoreServices.DevEnvServices.OpenDataDirectory(item.Key);
        if (!result.Ok)
            Message = result.DisplayMessage;
    }

    public void Dispose() => _timer.Stop();
}