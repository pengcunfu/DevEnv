using LavaTranslator.Models;
using LavaTranslator.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DevBox.Api;

public static class TranslatorEndpoints
{
    private static readonly ConfigService Config = new();

    public static void MapTranslatorEndpoints(this WebApplication app)
    {
        app.MapGet("/api/translator/sites", () =>
            Results.Json(WebTranslateCatalog.All.Select(s => new
            {
                id = s.Id,
                name = s.Name,
                homeUrl = s.HomeUrl.ToString(),
            })));

        app.MapGet("/api/translator/config", () =>
            Results.Json(new
            {
                lastWebSiteId = Config.Current.General.LastWebSiteId,
                configPath = Config.ConfigFilePath,
            }));

        app.MapPut("/api/translator/site", async (HttpRequest req) =>
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var id = doc.RootElement.TryGetProperty("id", out var p) ? p.GetString() : null;
            var open = doc.RootElement.TryGetProperty("open", out var o) && o.GetBoolean();
            var fillClipboard = doc.RootElement.TryGetProperty("fillClipboard", out var f) && f.GetBoolean();
            var site = WebTranslateCatalog.FindById(id) ?? WebTranslateCatalog.All[0];
            var general = Config.Current.General;
            general.LastWebSiteId = site.Id;
            Config.UpdateGeneral(general);

            if (open)
            {
                HostCallbacks.OnShowTranslator?.Invoke(new TranslatorRequest
                {
                    Visible = true,
                    SiteId = site.Id,
                    FillClipboard = fillClipboard,
                });
            }

            return Results.Json(new
            {
                id = site.Id,
                name = site.Name,
                homeUrl = site.HomeUrl.ToString(),
            });
        });

        app.MapGet("/api/translator/resolve-url", (string? siteId, string? text) =>
        {
            var site = WebTranslateCatalog.FindById(siteId) ?? WebTranslateCatalog.All[0];
            return Results.Json(new
            {
                id = site.Id,
                name = site.Name,
                url = site.ResolveUrl(text).ToString(),
            });
        });
    }
}
