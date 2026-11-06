using Newtonsoft.Json.Linq;

namespace DevEnv.Core.Models;

/// <summary>
/// 全局启动器的布局：每个页签的卡片顺序与「启动器文件夹」分组。
/// 快捷方式本身仍保存在 NativeAtomList / FolderAtomList / WebAtomList，这里只记录摆放方式。
/// </summary>
public sealed class LauncherLayout
{
    /// <summary>可编辑页签数量：本地应用 / 文件夹 / 网站；系统工具页只读，不进入布局。</summary>
    public const int EditablePageCount = 3;

    public List<LauncherLayoutPage> Pages { get; set; } = [];

    /// <summary>取指定页签的布局，不存在时创建。</summary>
    public LauncherLayoutPage Page(int page)
    {
        var existing = Pages.FirstOrDefault(p => p.Page == page);
        if (existing is not null)
            return existing;

        existing = new LauncherLayoutPage { Page = page };
        Pages.Add(existing);
        Pages.Sort((a, b) => a.Page.CompareTo(b.Page));
        return existing;
    }

    /// <summary>
    /// 与真实快捷方式对账：丢弃已不存在的条目、清理空组、把新增条目追加到页尾。
    /// 纯内存操作，调用方决定何时落盘。
    /// </summary>
    public void Normalize(IReadOnlyList<IReadOnlyList<string>> availableKeysByPage)
    {
        var result = new List<LauncherLayoutPage>();
        var usedGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var pageIndex = 0; pageIndex < EditablePageCount; pageIndex++)
        {
            var available = (pageIndex < availableKeysByPage.Count ? availableKeysByPage[pageIndex] : [])
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .ToList();
            var canonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in available)
                canonical.TryAdd(key, key);

            var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tiles = new List<LauncherLayoutTile>();
            var source = Pages.FirstOrDefault(p => p.Page == pageIndex)?.Tiles ?? [];

            // 第一遍只认领组内条目：同一 key 同时出现在独立卡片和组里时，组优先
            var groupItems = new Dictionary<LauncherLayoutTile, List<string>>();
            foreach (var tile in source.Where(t => t.IsGroup))
            {
                var items = new List<string>();
                foreach (var key in tile.Items)
                {
                    if (canonical.TryGetValue(key, out var real) && placed.Add(real))
                        items.Add(real);
                }
                groupItems[tile] = items;
            }

            // 第二遍按原顺序输出卡片，保持用户摆放的位置不被打乱
            foreach (var tile in source)
            {
                if (tile.IsGroup)
                {
                    var items = groupItems[tile];
                    if (items.Count >= 2)
                    {
                        var id = tile.Id;
                        if (string.IsNullOrWhiteSpace(id) || !usedGroupIds.Add(id))
                            id = Guid.NewGuid().ToString("N");
                        usedGroupIds.Add(id);
                        tiles.Add(new LauncherLayoutTile
                        {
                            Type = "group",
                            Id = id,
                            Name = string.IsNullOrWhiteSpace(tile.Name) ? "文件夹" : tile.Name,
                            Items = items,
                        });
                    }
                    else if (items.Count == 1)
                    {
                        tiles.Add(LauncherLayoutTile.Item(items[0]));
                    }
                    continue;
                }

                if (canonical.TryGetValue(tile.Key, out var canonicalKey) && placed.Add(canonicalKey))
                    tiles.Add(LauncherLayoutTile.Item(canonicalKey));
            }

            foreach (var key in available)
            {
                if (placed.Add(key))
                    tiles.Add(LauncherLayoutTile.Item(key));
            }

            result.Add(new LauncherLayoutPage { Page = pageIndex, Tiles = tiles });
        }

        Pages = result;
    }

    /// <summary>移动顶层卡片（普通条目或文件夹磁贴）到插入位置。</summary>
    public void MoveTiles(int page, IReadOnlyList<string> tileIds, int insertIndex)
    {
        var layoutPage = Page(page);
        MoveWithin(layoutPage.Tiles, tileIds, insertIndex, TileId);
    }

    /// <summary>移动文件夹内的条目到插入位置。</summary>
    public void MoveGroupItems(string groupId, IReadOnlyList<string> keys, int insertIndex)
    {
        var group = FindGroup(groupId);
        if (group is null)
            return;
        MoveWithin(group.Items, keys, insertIndex, key => key);
    }

    /// <summary>把若干顶层条目合并成一个文件夹磁贴，放在插入位置。</summary>
    public LauncherLayoutTile? CreateGroup(
        int page, string name, IReadOnlyList<string> keys, int insertIndex)
    {
        var layoutPage = Page(page);
        var unique = keys.Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (unique.Count < 2)
            return null;

        var wanted = new HashSet<string>(unique, StringComparer.OrdinalIgnoreCase);
        if (!layoutPage.Tiles.Any(t => !t.IsGroup && wanted.Contains(t.Key)))
            return null;

        var clamped = Math.Clamp(insertIndex, 0, layoutPage.Tiles.Count);
        var removedBefore = layoutPage.Tiles.Take(clamped)
            .Count(t => !t.IsGroup && wanted.Contains(t.Key));
        layoutPage.Tiles.RemoveAll(t => !t.IsGroup && wanted.Contains(t.Key));
        var insertAt = Math.Clamp(insertIndex - removedBefore, 0, layoutPage.Tiles.Count);

        var group = new LauncherLayoutTile
        {
            Type = "group",
            Id = Guid.NewGuid().ToString("N"),
            Name = string.IsNullOrWhiteSpace(name) ? "文件夹" : name.Trim(),
            Items = unique,
        };
        layoutPage.Tiles.Insert(insertAt, group);
        return group;
    }

    /// <summary>把顶层条目移入已有文件夹，条目从页面上移除。</summary>
    public void AddToGroup(string groupId, IReadOnlyList<string> keys)
    {
        var group = FindGroup(groupId);
        if (group is null)
            return;

        var wanted = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        var page = Pages.FirstOrDefault(p => p.Tiles.Contains(group));
        page?.Tiles.RemoveAll(t => !t.IsGroup && wanted.Contains(t.Key));

        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            if (!group.Items.Contains(key, StringComparer.OrdinalIgnoreCase))
                group.Items.Add(key);
        }
    }

    /// <summary>把条目移出文件夹，紧跟在文件夹磁贴后面；剩余不足 2 项时自动解散。</summary>
    public void MoveOutOfGroup(string groupId, IReadOnlyList<string> keys)
    {
        var group = FindGroup(groupId);
        if (group is null)
            return;

        var wanted = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        var moved = group.Items.Where(wanted.Contains).ToList();
        group.Items.RemoveAll(wanted.Contains);

        var layoutPage = Pages.FirstOrDefault(p => p.Tiles.Contains(group));
        if (layoutPage is null)
            return;

        if (group.Items.Count < 2)
        {
            var index = layoutPage.Tiles.IndexOf(group);
            layoutPage.Tiles.RemoveAt(index);
            layoutPage.Tiles.InsertRange(index, group.Items.Select(LauncherLayoutTile.Item));
            layoutPage.Tiles.InsertRange(index + group.Items.Count, moved.Select(LauncherLayoutTile.Item));
            return;
        }

        var position = layoutPage.Tiles.IndexOf(group) + 1;
        layoutPage.Tiles.InsertRange(position, moved.Select(LauncherLayoutTile.Item));
    }

    /// <summary>解散文件夹：条目按原顺序回到页面上原位置。</summary>
    public void DissolveGroup(string groupId)
    {
        var group = FindGroup(groupId);
        if (group is null)
            return;

        var layoutPage = Pages.FirstOrDefault(p => p.Tiles.Contains(group));
        if (layoutPage is null)
            return;

        var index = layoutPage.Tiles.IndexOf(group);
        layoutPage.Tiles.RemoveAt(index);
        layoutPage.Tiles.InsertRange(index, group.Items.Select(LauncherLayoutTile.Item));
    }

    /// <summary>重命名文件夹。</summary>
    public void RenameGroup(string groupId, string name)
    {
        var group = FindGroup(groupId);
        if (group is not null && !string.IsNullOrWhiteSpace(name))
            group.Name = name.Trim();
    }

    public LauncherLayoutTile? FindGroup(string groupId) =>
        Pages.SelectMany(p => p.Tiles)
            .FirstOrDefault(t => t.IsGroup && string.Equals(t.Id, groupId, StringComparison.OrdinalIgnoreCase));

    /// <summary>顶层卡片的稳定标识：普通条目用 key，文件夹用 group|id。</summary>
    public static string TileId(LauncherLayoutTile tile) =>
        tile.IsGroup ? $"group|{tile.Id}" : tile.Key;

    public static string AppKey(string path) => $"app|{path}";
    public static string FolderKey(string path) => $"folder|{path}";
    public static string WebKey(string url) => $"web|{url}";

    private static void MoveWithin<T>(
        List<T> items,
        IReadOnlyList<string> ids,
        int insertIndex,
        Func<T, string> idOf)
    {
        var wanted = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        if (wanted.Count == 0)
            return;

        var moving = new List<(T Item, int Index)>();
        for (var i = 0; i < items.Count; i++)
        {
            if (wanted.Contains(idOf(items[i])))
                moving.Add((items[i], i));
        }

        if (moving.Count == 0)
            return;

        var removedBefore = moving.Count(m => m.Index < insertIndex);
        items.RemoveAll(t => wanted.Contains(idOf(t)));
        var insertAt = Math.Clamp(insertIndex - removedBefore, 0, items.Count);

        // 保持拖动前的相对顺序
        items.InsertRange(insertAt, moving.Select(m => m.Item));
    }

    #region JSON 映射

    public static LauncherLayout FromJson(JArray json)
    {
        var layout = new LauncherLayout();
        foreach (var pageObject in json.OfType<JObject>())
        {
            var page = new LauncherLayoutPage
            {
                Page = pageObject["Page"]?.Value<int>() ?? 0,
            };

            if (pageObject["Tiles"] is JArray tiles)
            {
                foreach (var tileObject in tiles.OfType<JObject>())
                {
                    page.Tiles.Add(new LauncherLayoutTile
                    {
                        Type = tileObject["Type"]?.ToString() ?? "item",
                        Key = tileObject["Key"]?.ToString() ?? string.Empty,
                        Id = tileObject["Id"]?.ToString() ?? string.Empty,
                        Name = tileObject["Name"]?.ToString() ?? string.Empty,
                        Items = (tileObject["Items"] as JArray)?
                            .Select(item => item.ToString())
                            .Where(item => !string.IsNullOrWhiteSpace(item))
                            .ToList() ?? [],
                    });
                }
            }

            layout.Pages.Add(page);
        }

        return layout;
    }

    public JArray ToJson() => new(Pages
        .OrderBy(page => page.Page)
        .Select(page => new JObject
        {
            ["Page"] = page.Page,
            ["Tiles"] = new JArray(page.Tiles.Select(tile => tile.ToJson())),
        }));

    #endregion
}

/// <summary>启动器布局中的一页。</summary>
public sealed class LauncherLayoutPage
{
    public int Page { get; set; }
    public List<LauncherLayoutTile> Tiles { get; set; } = [];
}

/// <summary>布局中的一张卡片：普通条目或文件夹。</summary>
public sealed class LauncherLayoutTile
{
    public string Type { get; set; } = "item";
    public string Key { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> Items { get; set; } = [];

    public bool IsGroup => string.Equals(Type, "group", StringComparison.OrdinalIgnoreCase);

    public static LauncherLayoutTile Item(string key) => new() { Type = "item", Key = key };

    public JObject ToJson() => IsGroup
        ? new JObject
        {
            ["Type"] = "group",
            ["Id"] = Id,
            ["Name"] = Name,
            ["Items"] = new JArray(Items),
        }
        : new JObject
        {
            ["Type"] = "item",
            ["Key"] = Key,
        };
}
