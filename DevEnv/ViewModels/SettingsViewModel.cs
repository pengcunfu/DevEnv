using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core.Models;
using DevEnv.Core.Services;
using DevEnv.Host.Themes;

namespace DevEnv.Host.ViewModels;

/// <summary>设置页：常规选项（含深色主题）+ 默认翻译引擎 + 检查更新。</summary>
public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _updateTimer;

    public SettingsViewModel()
    {
        Config = CoreServices.Settings.GetConfig();
        Sites = [];
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _updateTimer.Tick += (_, _) =>
        {
            if (Update?.Downloading != true)
                return;
            Update = CoreServices.Update.GetStatus();
            if (Update.Downloading || Update.DownloadProgress < 100)
                return;
            _updateTimer.Stop();
        };

        LoadSites();
        Update = CoreServices.Update.GetStatus();
    }

    [ObservableProperty]
    private AppConfig _config = new();

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private UpdateStatusView? _update;

    [ObservableProperty]
    private string _updateMessage = string.Empty;

    public ObservableCollection<TranslateSiteInfo> Sites { get; }

    [ObservableProperty]
    private string _selectedSiteId = string.Empty;

    private void LoadSites()
    {
        Sites.Clear();
        foreach (var site in CoreServices.Translator.GetSites())
            Sites.Add(site);
        SelectedSiteId = CoreServices.Translator.GetLastSiteId();
        if (Sites.Count > 0 && Sites.All(s => s.Id != SelectedSiteId))
            SelectedSiteId = Sites[0].Id;
    }

    /// <summary>主题开关：立即生效并随「保存」一起落盘。</summary>
    partial void OnConfigChanged(AppConfig value)
    {
        if (ThemeManager.IsDark != value.DarkMode)
            ThemeManager.Apply(value.DarkMode);
    }

    [RelayCommand]
    private void Save()
    {
        var result = CoreServices.Settings.Save(Config);
        Message = result.DisplayMessage;
    }

    [RelayCommand]
    private void PickDefaultSite()
    {
        var site = CoreServices.Translator.SetDefaultSite(SelectedSiteId);
        Message = $"默认引擎已设为 {site.Name}";
    }

    [RelayCommand]
    private void OpenTranslatorWindow()
    {
        var site = CoreServices.Translator.SetDefaultSite(SelectedSiteId, open: true);
        Message = $"已请求打开 {site.Name}";
    }

    [RelayCommand]
    private async Task CheckUpdateAsync()
    {
        var result = await CoreServices.Update.CheckAsync();
        UpdateMessage = result.DisplayMessage;
        Update = CoreServices.Update.GetStatus();
    }

    [RelayCommand]
    private void DownloadUpdate()
    {
        var result = CoreServices.Update.StartDownload();
        UpdateMessage = result.Ok ? "开始下载…" : result.DisplayMessage;
        _updateTimer.Start();
        Update = CoreServices.Update.GetStatus();
    }

    [RelayCommand]
    private void ApplyUpdate()
    {
        var result = CoreServices.Update.ApplyAndRestart();
        UpdateMessage = result.DisplayMessage;
    }

    public void Dispose() => _updateTimer.Stop();
}