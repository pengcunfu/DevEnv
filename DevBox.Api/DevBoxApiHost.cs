using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;

namespace DevBox.Api;

public sealed class DevBoxApiHost : IAsyncDisposable
{
    private WebApplication? _app;
    private readonly ConfigStore _config = new();

    public string SessionToken => SessionAuth.Token;
    public int ApiPort { get; private set; } = ApiConstants.ApiPort;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        SessionAuth.Reset();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = Array.Empty<string>(),
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.WebHost.UseUrls(ApiConstants.ApiBaseUrl);
        builder.Services.AddSingleton(_config);
        builder.Services.AddSingleton<DataStore>();
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
                policy.WithOrigins(ApiConstants.UiBaseUrl, "http://localhost:35173")
                    .AllowAnyHeader()
                    .AllowAnyMethod());
        });

        _app = builder.Build();
        _app.UseCors();
        _app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api")
                && !ctx.Request.Path.Equals("/api/health", StringComparison.OrdinalIgnoreCase)
                && !SessionAuth.IsAuthorized(ctx))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsJsonAsync(new { error = "unauthorized" });
                return;
            }

            await next();
        });

        MapEndpoints(_app);
        _app.MapProcessEndpoints();
        _app.MapShortcutEndpoints();
        _app.MapNetworkEndpoints();
        _app.MapNetworkExtraEndpoints();
        _app.MapLavaEndpoints();
        _app.MapLavaExtraEndpoints();
        _app.MapClipboardEndpoints();
        _app.MapTranslatorEndpoints();
        _app.MapUtilityEndpoints();
        _app.MapStartupEndpoints();
        _app.MapUpdateEndpoints();
        _app.MapDevEnvEndpoints();
        _app.MapMediaEndpoints();

        var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        if (Directory.Exists(wwwroot))
        {
            _app.UseDefaultFiles(new DefaultFilesOptions
            {
                FileProvider = new PhysicalFileProvider(wwwroot),
            });
            _app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(wwwroot),
            });
        }

        // 系统工具图标（DevBox/Assets/Icons/win → 输出 Resources/icons/win）
        var toolIcons = Path.Combine(AppContext.BaseDirectory, "Resources", "icons");
        if (Directory.Exists(toolIcons))
        {
            _app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(toolIcons),
                RequestPath = "/icons",
            });
        }

        await _app.StartAsync(cancellationToken);
        ApiPort = ApiConstants.ApiPort;
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is null)
            return;
        await _app.StopAsync();
        await _app.DisposeAsync();
        _app = null;
    }

    private void MapEndpoints(WebApplication app)
    {
        app.MapGet("/api/health", () => Results.Json(new
        {
            ok = true,
            product = "程序盒子",
            version = typeof(DevBoxApiHost).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            uiPort = ApiConstants.UiPort,
            apiPort = ApiConstants.ApiPort,
        }));

        app.MapGet("/api/session", (HttpContext ctx) =>
        {
            if (!SessionAuth.IsAuthorized(ctx))
                return Results.Unauthorized();
            return Results.Json(new { token = SessionAuth.Token });
        });

        app.MapGet("/api/config", (ConfigStore store) =>
            Results.Content(store.GetConfig().ToString(), "application/json", Encoding.UTF8));

        app.MapPut("/api/config", async (HttpContext ctx, ConfigStore store) =>
        {
            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            var patch = string.IsNullOrWhiteSpace(body) ? new JObject() : JObject.Parse(body);
            var saved = store.SaveConfig(patch);
            if (saved["SelfStarting"] is not null)
                AutostartHelper.Set(saved.Value<bool?>("SelfStarting") == true);
            if (saved["TrayIcon"] is not null)
                HostCallbacks.OnTrayIconChanged?.Invoke(saved.Value<bool?>("TrayIcon") != false);
            if (saved["ShowWindowHotkey"] is not null)
                HostCallbacks.OnShowWindowHotkeyChanged?.Invoke(saved.Value<string>("ShowWindowHotkey"));
            return Results.Content(saved.ToString(), "application/json", Encoding.UTF8);
        });

        app.MapPost("/api/shell/pick-exe", () =>
        {
            var path = HostCallbacks.PickExecutable?.Invoke();
            return path is null
                ? Results.Json(new { cancelled = true })
                : Results.Json(new { cancelled = false, path });
        });

        app.MapPost("/api/shell/pick-folder", () =>
        {
            var path = HostCallbacks.PickFolder?.Invoke();
            return path is null
                ? Results.Json(new { cancelled = true })
                : Results.Json(new { cancelled = false, path });
        });

        app.MapPost("/api/shell/pick-files", async (HttpRequest req) =>
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var filter = doc.RootElement.TryGetProperty("filter", out var f)
                ? f.GetString() ?? "所有文件|*.*"
                : "所有文件|*.*";
            var files = HostCallbacks.PickFiles?.Invoke(filter);
            return files is null
                ? Results.Json(new { cancelled = true, files = Array.Empty<string>() })
                : Results.Json(new { cancelled = false, files });
        });

        app.MapPost("/api/shell/activate-tab", async (HttpRequest req) =>
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var tab = doc.RootElement.TryGetProperty("tab", out var p) ? p.GetString() : null;
            HostCallbacks.OnActivateTab?.Invoke(tab);
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/shell/show-translator", async (HttpRequest req) =>
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var url = doc.RootElement.TryGetProperty("url", out var p) ? p.GetString() : null;
            var visible = !doc.RootElement.TryGetProperty("visible", out var v) || v.GetBoolean();
            var siteId = doc.RootElement.TryGetProperty("siteId", out var s) ? s.GetString() : null;
            var fillClipboard = doc.RootElement.TryGetProperty("fillClipboard", out var f) && f.GetBoolean();
            var text = doc.RootElement.TryGetProperty("text", out var t) ? t.GetString() : null;
            HostCallbacks.OnShowTranslator?.Invoke(new TranslatorRequest
            {
                Visible = visible,
                Url = url,
                SiteId = siteId,
                FillClipboard = fillClipboard,
                Text = text,
            });
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/shell/exit", () =>
        {
            HostCallbacks.OnExitApp?.Invoke();
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/shell/show-clipboard", async (HttpRequest req) =>
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var visible = !doc.RootElement.TryGetProperty("visible", out var v) || v.GetBoolean();
            HostCallbacks.OnShowClipboard?.Invoke(visible);
            return Results.Ok(new { ok = true });
        });

        app.MapGet("/api/nav", () => Results.Json(new
        {
            items = new[]
            {
                new { id = "apps", title = "本地应用" },
                new { id = "folders", title = "文件夹" },
                new { id = "webs", title = "网站" },
                new { id = "devenv", title = "开发环境" },
                new { id = "lava", title = "熔岩环境" },
                new { id = "network", title = "网络监控" },
                new { id = "processes", title = "进程监控" },
                new { id = "tools", title = "系统工具" },
            }
        }));
    }
}
