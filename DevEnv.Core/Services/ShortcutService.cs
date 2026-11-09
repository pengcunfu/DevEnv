using System.Diagnostics;
using DevEnv.Core.Models;
using Newtonsoft.Json.Linq;

namespace DevEnv.Core.Services;

/// <summary>
/// 本地应用 / 文件夹 / 网站三类快捷方式。逻辑从 <c>ShortcutEndpoints</c> 原样平移，
/// 数据仍读写 data.json 的 NativeAtomList / FolderAtomList / WebAtomList。
/// </summary>
public sealed class ShortcutService
{
    private const string NativeKey = "NativeAtomList";
    private const string FolderKey = "FolderAtomList";
    private const string WebKey = "WebAtomList";
    private const string LauncherLayoutKey = "LauncherLayout";

    /// <summary>实用工具已迁到顶部菜单，系统工具页只保留 Windows 系统入口。</summary>
    private static readonly HashSet<string> MenuOnlyKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "json_formatter", "hosts_editor", "image_converter", "hash_calculator", "startup",
        // SigXor 模块的工具同样在顶部菜单里有入口，不在系统工具页重复列出
        "file_preview", "sigxor_launcher", "sigxor_browser", "screenshot", "sigxor_settings",
    };

    private readonly DataStore _store;
    private readonly StartMenuService _startMenu;

    /// <summary>应用列表的读取 / 写入锁：启动后台补建开始菜单快捷方式时不能和 UI 添加互相覆盖。</summary>
    private readonly object _gate = new();

    public ShortcutService(DataStore store, StartMenuService startMenu)
    {
        _store = store;
        _startMenu = startMenu;
    }

    #region 本地应用

    public IReadOnlyList<NativeAppItem> GetApps()
    {
        lock (_gate)
            return ReadApps();
    }

    /// <summary>
    /// 添加本地应用。默认同时确保开始菜单里有对应快捷方式；
    /// 从开始菜单页收进启动器时传 <paramref name="syncStartMenu"/> = false，避免重复创建。
    /// </summary>
    public OperationResult AddApp(
        string? path,
        string? nameOverride = null,
        string? iconOverride = null,
        bool syncStartMenu = true)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return OperationResult.Fail("无效的 exe 路径");

            var name = string.IsNullOrWhiteSpace(nameOverride)
                ? IconHelper.GetDisplayName(path)
                : nameOverride;
            var icon = string.IsNullOrWhiteSpace(iconOverride)
                ? IconHelper.ExtractExecutableIcon(path) ?? string.Empty
                : iconOverride;

            var list = ReadApps();
            var existing = list.FirstOrDefault(x =>
                string.Equals(x.ExecPath, path, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.Name = name;
                existing.Description = path;
                if (string.IsNullOrWhiteSpace(existing.IconPath))
                    existing.IconPath = icon;
            }
            else
            {
                existing = new NativeAppItem
                {
                    Name = name,
                    IconPath = icon,
                    Description = path,
                    ExecPath = path,
                };
                list.Add(existing);
            }

            if (syncStartMenu)
                SyncStartMenuShortcut(existing);
            else
                existing.StartMenuChecked = true;

            _store.SaveList(NativeKey, new JArray(list.Select(WriteApp)));
            return OperationResult.Success(name);
        }
    }

    /// <summary>确保单条应用有开始菜单快捷方式；空串 = 外部已有 / 源本身是快捷方式，null = 失败待重试。</summary>
    private void SyncStartMenuShortcut(NativeAppItem item)
    {
        var link = _startMenu.EnsureShortcut(item.ExecPath, item.Name, item.StartMenuLink);
        if (link is null)
            return;

        item.StartMenuLink = link;
        item.StartMenuChecked = true;
    }

    /// <summary>
    /// 启动时后台补建：只处理还没检查过、或托管文件被手动删掉的条目。
    /// 返回实际创建 / 复用托管快捷方式的条目数。
    /// </summary>
    public int EnsureStartMenuShortcuts()
    {
        lock (_gate)
        {
            var list = ReadApps();
            var changed = false;
            var synced = 0;

            foreach (var item in list)
            {
                if (string.IsNullOrWhiteSpace(item.ExecPath) || !File.Exists(item.ExecPath))
                    continue;

                var linkMissing = !string.IsNullOrWhiteSpace(item.StartMenuLink)
                    && !File.Exists(Path.Combine(_startMenu.ManagedFolderPath, item.StartMenuLink));
                if (item.StartMenuChecked && !linkMissing)
                    continue;

                var link = _startMenu.EnsureShortcut(item.ExecPath, item.Name, item.StartMenuLink);
                if (link is null)
                    continue;

                item.StartMenuLink = link;
                item.StartMenuChecked = true;
                changed = true;
                synced++;
            }

            if (changed)
                _store.SaveList(NativeKey, new JArray(list.Select(WriteApp)));

            return synced;
        }
    }

    /// <summary>把开始菜单里的一条快捷方式收进启动器：网址进网站页、目录进文件夹页，其余按应用处理。</summary>
    public OperationResult AddFromStartMenu(StartMenuItem? item)
    {
        if (item is null)
            return OperationResult.Fail("快捷方式不存在");

        lock (_gate)
        {
            if (item.Kind == StartMenuLinkKind.InternetShortcut)
            {
                if (string.IsNullOrWhiteSpace(item.TargetPath))
                    return OperationResult.Fail("网址无效");
                return AddWebCore(item.TargetPath, item.Name, item.IconPath);
            }

            var target = !string.IsNullOrWhiteSpace(item.TargetPath) && File.Exists(item.TargetPath)
                ? item.TargetPath
                : item.LinkPath;

            if (Directory.Exists(target))
                return AddFolderCore(target, item.Name);

            // 源快捷方式已经在开始菜单里，不再创建托管副本；图标优先从目标文件重新提取
            return AddApp(target, item.Name, iconOverride: null, syncStartMenu: false);
        }
    }

    public OperationResult UpdateApp(
        int index,
        string? name = null,
        string? execPath = null,
        string? iconPath = null,
        bool refreshIcon = false)
    {
        lock (_gate)
        {
            var list = ReadApps();
            if (index < 0 || index >= list.Count)
                return OperationResult.Fail("条目不存在");

            var item = list[index];
            if (name is not null)
            {
                item.Name = name;
                if (!string.IsNullOrWhiteSpace(item.StartMenuLink))
                {
                    var renamed = _startMenu.RenameManagedShortcut(item.StartMenuLink, name);
                    if (!string.IsNullOrWhiteSpace(renamed))
                        item.StartMenuLink = renamed;
                }
            }
            if (!string.IsNullOrWhiteSpace(execPath) && File.Exists(execPath))
            {
                item.ExecPath = execPath;
                item.Description = execPath;
            }
            if (iconPath is not null)
                item.IconPath = iconPath;
            if (refreshIcon && !string.IsNullOrWhiteSpace(item.ExecPath))
                item.IconPath = IconHelper.ExtractExecutableIcon(item.ExecPath) ?? item.IconPath;

            if (!item.StartMenuChecked)
                SyncStartMenuShortcut(item);

            _store.SaveList(NativeKey, new JArray(list.Select(WriteApp)));
            return OperationResult.Success();
        }
    }

    public OperationResult RunApp(int index)
    {
        var list = ReadApps();
        if (index < 0 || index >= list.Count)
            return OperationResult.Fail("条目不存在");

        var path = list[index].ExecPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return OperationResult.Fail("可执行文件不存在");

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory,
            });
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    public OperationResult OpenAppFolder(int index)
    {
        var list = ReadApps();
        if (index < 0 || index >= list.Count)
            return OperationResult.Fail("条目不存在");

        var path = list[index].ExecPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return OperationResult.Fail("可执行文件不存在");

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true,
            });
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    public OperationResult RemoveApp(int index)
    {
        lock (_gate)
        {
            var list = ReadApps();
            if (index < 0 || index >= list.Count)
                return OperationResult.Fail("条目不存在");

            var removed = list[index];
            list.RemoveAt(index);
            _store.SaveList(NativeKey, new JArray(list.Select(WriteApp)));

            // 只删我们托管的快捷方式；外部安装器创建的同目标快捷方式不受影响
            _startMenu.RemoveManagedShortcut(removed.StartMenuLink, removed.ExecPath);
            return OperationResult.Success();
        }
    }

    #endregion

    #region 文件夹

    public IReadOnlyList<FolderItem> GetFolders() => ReadFolders();

    public OperationResult AddFolder(string? path)
    {
        lock (_gate)
            return AddFolderCore(path, null);
    }

    private OperationResult AddFolderCore(string? path, string? nameOverride)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return OperationResult.Fail("无效的文件夹路径");

        var list = ReadFolders();
        var duplicate = list.FirstOrDefault(x =>
            string.Equals(x.FolderPath, path, StringComparison.OrdinalIgnoreCase));
        if (duplicate is not null)
            return OperationResult.Success(duplicate.Name);

        var name = string.IsNullOrWhiteSpace(nameOverride)
            ? new DirectoryInfo(path).Name
            : nameOverride;
        list.Add(new FolderItem
        {
            Name = name,
            Description = path,
            FolderPath = path,
        });
        _store.SaveList(FolderKey, new JArray(list.Select(WriteFolder)));
        return OperationResult.Success(name);
    }

    public OperationResult UpdateFolder(int index, string? name = null, string? folderPath = null)
    {
        var list = ReadFolders();
        if (index < 0 || index >= list.Count)
            return OperationResult.Fail("条目不存在");

        var item = list[index];
        if (name is not null)
            item.Name = name;
        if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
        {
            item.FolderPath = folderPath;
            item.Description = folderPath;
        }

        _store.SaveList(FolderKey, new JArray(list.Select(WriteFolder)));
        return OperationResult.Success();
    }

    public OperationResult OpenFolder(int index)
    {
        var list = ReadFolders();
        if (index < 0 || index >= list.Count)
            return OperationResult.Fail("条目不存在");

        var path = list[index].FolderPath;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return OperationResult.Fail("文件夹不存在");

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = true,
            });
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    public OperationResult RemoveFolder(int index)
    {
        var list = ReadFolders();
        if (index < 0 || index >= list.Count)
            return OperationResult.Fail("条目不存在");
        list.RemoveAt(index);
        _store.SaveList(FolderKey, new JArray(list.Select(WriteFolder)));
        return OperationResult.Success();
    }

    #endregion

    #region 网站

    public IReadOnlyList<WebItem> GetWebs() => ReadWebs();

    public async Task<OperationResult> AddWebAsync(string? urlInput, string? titleOverride = null)
    {
        if (string.IsNullOrWhiteSpace(urlInput))
            return OperationResult.Fail("缺少 url");

        string url;
        string title;
        string iconPath;
        try
        {
            (url, title, iconPath) = await WebMetadataHelper.FetchAsync(urlInput);
        }
        catch (Exception ex)
        {
            var normalized = WebMetadataHelper.NormalizeUrl(urlInput);
            if (normalized is null)
                return OperationResult.Fail(ex.Message);
            url = normalized;
            title = new Uri(url).Host;
            iconPath = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(titleOverride))
            title = titleOverride;

        lock (_gate)
            return AddWebCore(url, title, iconPath);
    }

    private OperationResult AddWebCore(string? url, string name, string? iconPath)
    {
        if (string.IsNullOrWhiteSpace(url))
            return OperationResult.Fail("缺少 url");

        var list = ReadWebs();
        var normalized = StartMenuIndex.NormalizeUrlForCompare(url);
        var duplicate = list.FirstOrDefault(x =>
            string.Equals(StartMenuIndex.NormalizeUrlForCompare(x.Url), normalized, StringComparison.OrdinalIgnoreCase));
        if (duplicate is not null)
            return OperationResult.Success(duplicate.Name);

        list.Add(new WebItem
        {
            Name = name,
            IconPath = iconPath ?? string.Empty,
            Description = url,
            Url = url,
        });
        _store.SaveList(WebKey, new JArray(list.Select(WriteWeb)));
        return OperationResult.Success(name);
    }

    public async Task<OperationResult> UpdateWebAsync(
        int index,
        string? name = null,
        string? urlInput = null,
        bool refreshIcon = false)
    {
        var list = ReadWebs();
        if (index < 0 || index >= list.Count)
            return OperationResult.Fail("条目不存在");

        var item = list[index];
        if (name is not null)
            item.Name = name;
        if (!string.IsNullOrWhiteSpace(urlInput))
        {
            var url = WebMetadataHelper.NormalizeUrl(urlInput);
            if (url is not null)
            {
                item.Url = url;
                item.Description = url;
            }
        }
        if (refreshIcon)
        {
            try
            {
                var (_, _, icon) = await WebMetadataHelper.FetchAsync(item.Url);
                item.IconPath = icon;
            }
            catch
            {
                // 图标抓取失败保持原值
            }
        }

        _store.SaveList(WebKey, new JArray(list.Select(WriteWeb)));
        return OperationResult.Success();
    }

    public OperationResult OpenWeb(int index)
    {
        var list = ReadWebs();
        if (index < 0 || index >= list.Count)
            return OperationResult.Fail("条目不存在");

        var url = list[index].Url;
        if (string.IsNullOrWhiteSpace(url))
            return OperationResult.Fail("网址无效");

        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    public OperationResult RemoveWeb(int index)
    {
        var list = ReadWebs();
        if (index < 0 || index >= list.Count)
            return OperationResult.Fail("条目不存在");
        list.RemoveAt(index);
        _store.SaveList(WebKey, new JArray(list.Select(WriteWeb)));
        return OperationResult.Success();
    }

    #endregion

    #region Windows 系统工具

    public IReadOnlyList<ToolInfo> GetSystemTools() =>
        WindowsToolRunner.Catalog
            .Where(t => !MenuOnlyKeys.Contains(t.Key))
            .Select(t => new ToolInfo
            {
                Key = t.Key,
                Name = t.Name,
                Category = t.Category,
                Description = t.Description,
                IconPath = SystemToolIcons.ResolveIconPath(t.Key),
            })
            .ToList();

    public OperationResult RunSystemTool(string key) =>
        WindowsToolRunner.TryRun(key, out var error)
            ? OperationResult.Success()
            : OperationResult.Fail(error ?? "启动失败");

    #endregion

    #region 启动器布局

    /// <summary>读取全局启动器的卡片顺序与文件夹分组；未保存过时返回空布局，由调用方对账。</summary>
    public LauncherLayout ReadLauncherLayout() =>
        LauncherLayout.FromJson(_store.GetList(LauncherLayoutKey));

    /// <summary>保存全局启动器的卡片顺序与文件夹分组。</summary>
    public void SaveLauncherLayout(LauncherLayout layout) =>
        _store.SaveList(LauncherLayoutKey, layout.ToJson());

    #endregion

    #region data.json 映射

    private List<NativeAppItem> ReadApps() =>
        _store.GetList(NativeKey).OfType<JObject>().Select(ReadApp).ToList();

    private List<FolderItem> ReadFolders() =>
        _store.GetList(FolderKey).OfType<JObject>().Select(ReadFolder).ToList();

    private List<WebItem> ReadWebs() =>
        _store.GetList(WebKey).OfType<JObject>().Select(ReadWeb).ToList();

    private static NativeAppItem ReadApp(JObject o) => new()
    {
        Name = o["Name"]?.ToString() ?? string.Empty,
        IconPath = IconHelper.ResolveIconPath(o["IconPath"]?.ToString()) ?? string.Empty,
        Description = o["Description"]?.ToString() ?? string.Empty,
        ExecPath = o["ExecPath"]?.ToString() ?? string.Empty,
        StartMenuLink = o["StartMenuLink"]?.ToString() ?? string.Empty,
        StartMenuChecked = o["StartMenuChecked"]?.Value<bool>() ?? false,
    };

    private static JObject WriteApp(NativeAppItem item) => new()
    {
        ["Name"] = item.Name,
        ["IconPath"] = item.IconPath,
        ["Tag"] = "native",
        ["Description"] = item.Description,
        ["ExecPath"] = item.ExecPath,
        ["StartMenuLink"] = item.StartMenuLink,
        ["StartMenuChecked"] = item.StartMenuChecked,
    };

    private static FolderItem ReadFolder(JObject o) => new()
    {
        Name = o["Name"]?.ToString() ?? string.Empty,
        IconPath = IconHelper.ResolveIconPath(o["IconPath"]?.ToString()) ?? string.Empty,
        Description = o["Description"]?.ToString() ?? string.Empty,
        FolderPath = o["FolderPath"]?.ToString() ?? string.Empty,
    };

    private static JObject WriteFolder(FolderItem item) => new()
    {
        ["Name"] = item.Name,
        ["IconPath"] = item.IconPath,
        ["Tag"] = "folder",
        ["Description"] = item.Description,
        ["FolderPath"] = item.FolderPath,
    };

    private static WebItem ReadWeb(JObject o) => new()
    {
        Name = o["Name"]?.ToString() ?? string.Empty,
        IconPath = IconHelper.ResolveIconPath(o["IconPath"]?.ToString()) ?? string.Empty,
        Description = o["Description"]?.ToString() ?? string.Empty,
        Url = o["Url"]?.ToString() ?? string.Empty,
    };

    private static JObject WriteWeb(WebItem item) => new()
    {
        ["Name"] = item.Name,
        ["IconPath"] = item.IconPath,
        ["Tag"] = "web",
        ["Description"] = item.Description,
        ["Url"] = item.Url,
    };

    #endregion
}

/// <summary>系统工具图标路径（发布产物里是 Resources/icons/win/&lt;key&gt;.png）。</summary>
public static class SystemToolIcons
{
    public static string Directory { get; } = Path.Combine(AppContext.BaseDirectory, "Resources", "icons", "win");

    public static string GetIconPath(string key) => Path.Combine(Directory, $"{key}.png");

    public static string FallbackIconPath => Path.Combine(Directory, "default.png");

    /// <summary>工具图标路径；缺图时回落到 default.png（宿主只需绑定这一个值）。</summary>
    public static string ResolveIconPath(string key)
    {
        var path = GetIconPath(key);
        return File.Exists(path) ? path : FallbackIconPath;
    }
}