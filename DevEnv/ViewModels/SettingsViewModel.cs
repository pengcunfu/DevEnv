using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core.Models;
using DevEnv.Core.Services;
using DevEnv.Host.Themes;

namespace DevEnv.Host.ViewModels;

/// <summary>设置页：常规选项（含深色主题）+ 默认翻译引擎。检查更新位于「关于与帮助」页。</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    public SettingsViewModel()
    {
        Config = CoreServices.Settings.GetConfig();
        Sites = [];
        LoadSites();
    }

    [ObservableProperty]
    private AppConfig _config = new();

    [ObservableProperty]
    private string _message = string.Empty;

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

    /// <summary>重新读取配置（从「快捷键」页切回常规页时刷新，避免旧值覆盖）。</summary>
    public void Reload() => Config = CoreServices.Settings.GetConfig();

    [RelayCommand]
    private void Save()
    {
        // 快捷键由「快捷键」页单独保存，这里只写常规字段
        var result = CoreServices.Settings.SaveGeneral(Config);
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

}