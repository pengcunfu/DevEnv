using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core.Models;
using DevEnv.Core.Services;
using DevEnv.Host.Infrastructure;
using DevEnv.Host.Windows;

namespace DevEnv.Host.ViewModels;

/// <summary>开发环境页：用户自定义后台服务的配置、启停与目录快捷访问。</summary>
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

    [RelayCommand]
    public void Reload()
    {
        Services.Clear();
        foreach (var item in CoreServices.DevEnvServices.GetServices())
            Services.Add(item);
    }

    [RelayCommand]
    private void AddService()
    {
        var config = DevServiceEditWindow.Show(HostServices.DialogOwner);
        if (config is not null)
            Save(config);
    }

    [RelayCommand]
    private void EditService(DevServiceItem? item)
    {
        if (item is null)
            return;

        var config = CoreServices.DevEnvServices.GetConfig(item.Id);
        if (config is null)
        {
            HostServices.SetStatus("配置不存在，可能已被移除");
            Reload();
            return;
        }

        var edited = DevServiceEditWindow.Show(HostServices.DialogOwner, config);
        if (edited is not null)
            Save(edited);
    }

    [RelayCommand]
    private async Task RemoveService(DevServiceItem? item)
    {
        if (item is null)
            return;

        var prompt = item.Running
            ? $"移除「{item.Name}」？它会先被停止。"
            : $"移除「{item.Name}」？";
        if (!ConfirmDialog.Show(HostServices.DialogOwner, prompt, "移除"))
            return;

        if (item.Running)
        {
            var stop = await Task.Run(() => CoreServices.DevEnvServices.Stop(item.Id));
            if (!stop.Ok)
            {
                // 停不下来就先别移除，免得配置没了进程还留在后台。
                HostServices.SetStatus(stop.DisplayMessage);
                Reload();
                return;
            }
        }

        HostServices.SetStatus(CoreServices.DevEnvServices.Remove(item.Id).DisplayMessage);
        Reload();
    }

    // 启停要等进程退出 / 停止命令执行完，放到线程池避免卡住界面。
    [RelayCommand]
    private async Task Start(DevServiceItem? item)
    {
        if (item is null)
            return;

        var result = await Task.Run(() => CoreServices.DevEnvServices.Start(item.Id));
        HostServices.SetStatus(result.DisplayMessage);
        Reload();
    }

    [RelayCommand]
    private async Task Stop(DevServiceItem? item)
    {
        if (item is null)
            return;

        var result = await Task.Run(() => CoreServices.DevEnvServices.Stop(item.Id));
        HostServices.SetStatus(result.DisplayMessage);
        Reload();
    }

    [RelayCommand]
    private void OpenDirectory(DevServiceItem? item)
    {
        if (item is null)
            return;

        var result = CoreServices.DevEnvServices.OpenWorkingDirectory(item.Id);
        if (!result.Ok)
            HostServices.SetStatus(result.DisplayMessage);
    }

    [RelayCommand]
    private void OpenLog(DevServiceItem? item)
    {
        if (item is null)
            return;

        DevServiceLogWindow.Show(HostServices.DialogOwner, CoreServices.DevEnvServices, item.Id, item.Name);
    }

    [RelayCommand]
    private void OpenLogsDirectory()
    {
        var result = CoreServices.DevEnvServices.OpenLogsDirectory();
        if (!result.Ok)
            HostServices.SetStatus(result.DisplayMessage);
    }

    private void Save(DevServiceConfig config)
    {
        HostServices.SetStatus(CoreServices.DevEnvServices.Save(config).DisplayMessage);
        Reload();
    }

    public void Dispose() => _timer.Stop();
}
