using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBox.Core.Models;
using DevBox.Core.Services;

namespace DevBox.Host.ViewModels;

/// <summary>主壳里的熔岩翻译入口页：翻译站本体在独立窗里运行，这里只负责唤起与选默认引擎。</summary>
public sealed partial class TranslatorEntryViewModel : ObservableObject
{
    public TranslatorEntryViewModel()
    {
        Sites = [];
        Load();
    }

    public ObservableCollection<TranslateSiteInfo> Sites { get; }

    [ObservableProperty]
    private string _currentSiteId = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    private void Load()
    {
        Sites.Clear();
        foreach (var site in CoreServices.Translator.GetSites())
            Sites.Add(site);

        CurrentSiteId = CoreServices.Translator.GetLastSiteId();
        if (Sites.Count > 0 && Sites.All(s => s.Id != CurrentSiteId))
            CurrentSiteId = Sites[0].Id;
    }

    partial void OnCurrentSiteIdChanged(string value)
    {
        var site = Sites.FirstOrDefault(s => s.Id == value);
        if (site is not null)
            Message = $"默认引擎已设为 {site.Name}";
    }

    [RelayCommand]
    public void SetDefaultSite()
    {
        var site = CoreServices.Translator.SetDefaultSite(CurrentSiteId);
        Message = $"默认引擎已设为 {site.Name}";
    }

    [RelayCommand]
    private void OpenWindow() => Open(fillClipboard: false);

    [RelayCommand]
    private void OpenWithClipboard() => Open(fillClipboard: true);

    [RelayCommand]
    private void HideWindow()
    {
        TranslatorService.HideTranslator();
        Message = "已隐藏翻译窗口";
    }

    private void Open(bool fillClipboard)
    {
        var site = CoreServices.Translator.SetDefaultSite(CurrentSiteId, open: true, fillClipboard);
        Message = fillClipboard
            ? $"已打开翻译窗口并尝试填充到 {site.Name}"
            : $"已打开翻译窗口（{site.Name}）";
    }
}