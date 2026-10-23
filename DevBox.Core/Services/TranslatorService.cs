using DevBox.Core.Models;
using LavaTranslator.Models;
using LavaTranslator.Services;

namespace DevBox.Core.Services;

/// <summary>熔岩翻译：站点目录、默认引擎、翻译链接拼装。逻辑来自 <c>TranslatorEndpoints</c>。</summary>
public sealed class TranslatorService
{
    private readonly ConfigService _config = new();

    public IReadOnlyList<TranslateSiteInfo> GetSites() =>
        WebTranslateCatalog.All.Select(s => new TranslateSiteInfo
        {
            Id = s.Id,
            Name = s.Name,
            HomeUrl = s.HomeUrl.ToString(),
        }).ToList();

    public string ConfigFilePath => _config.ConfigFilePath;

    public string GetLastSiteId() => _config.Current.General.LastWebSiteId;

    /// <summary>切换默认引擎；<paramref name="open"/> 为 true 时顺带唤起翻译窗。</summary>
    public TranslateSiteInfo SetDefaultSite(string? id, bool open = false, bool fillClipboard = false)
    {
        var site = WebTranslateCatalog.FindById(id) ?? WebTranslateCatalog.All[0];
        var general = _config.Current.General;
        general.LastWebSiteId = site.Id;
        _config.UpdateGeneral(general);

        if (open)
        {
            HostCallbacks.OnShowTranslator?.Invoke(new TranslatorRequest
            {
                Visible = true,
                SiteId = site.Id,
                FillClipboard = fillClipboard,
            });
        }

        return new TranslateSiteInfo
        {
            Id = site.Id,
            Name = site.Name,
            HomeUrl = site.HomeUrl.ToString(),
        };
    }

    public TranslateSiteInfo Resolve(string? siteId, string? text)
    {
        var site = WebTranslateCatalog.FindById(siteId) ?? WebTranslateCatalog.All[0];
        return new TranslateSiteInfo
        {
            Id = site.Id,
            Name = site.Name,
            HomeUrl = site.ResolveUrl(text).ToString(),
        };
    }

    /// <summary>唤起 / 隐藏翻译窗（由菜单、热键、入口页调用）。</summary>
    public static void ShowTranslator(TranslatorRequest request) =>
        HostCallbacks.OnShowTranslator?.Invoke(request);

    public static void HideTranslator() =>
        HostCallbacks.OnShowTranslator?.Invoke(new TranslatorRequest { Visible = false });
}