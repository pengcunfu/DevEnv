using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using DevEnv.Core.Clipboard.Models;
using DevEnv.Core.Models;

namespace DevEnv.Core.Clipboard.Services;

/// <summary>
/// Owns the in-memory <see cref="Entries"/> list that the UI binds to, and
/// delegates persistence to <see cref="HistoryStore"/>. The collection holds
/// only the currently visible window — the underlying SQLite database is the
/// source of truth and may contain more rows.
/// </summary>
public sealed class HistoryService
{
    /// <summary>历史记录只保留最近三天，避免长期复制导致数据库与图片无限增长。</summary>
    public const int RetentionDays = 3;

    private static readonly TimeSpan PurgeInterval = TimeSpan.FromHours(1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HistoryStore _store;
    private DateTime _lastPurge = DateTime.MinValue;

    public ObservableCollection<ClipboardEntry> Entries { get; } = [];

    public HistoryService()
    {
        _store = new HistoryStore(AppPaths.HistoryDb);
        _store.Open();
        MigrateLegacyJson();

        try
        {
            PurgeExpired();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"清理过期剪贴板记录失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Reload the in-memory visible window from the database.
    /// </summary>
    public void ReloadView(HistoryQuery query)
    {
        // 打开/刷新列表时顺带清理过期记录，避免暂停监听后旧数据长期滞留。
        PurgeIfDue();

        var fresh = _store.Query(query);
        Entries.Clear();
        foreach (var e in fresh)
            Entries.Add(e);
    }

    /// <summary>
    /// Persist <paramref name="entry"/>; if the same content already exists,
    /// update its timestamp instead of creating a duplicate. Returns the row
    /// that ended up in the database (with its <see cref="ClipboardEntry.Id"/>
    /// populated). The caller is responsible for refreshing the visible window.
    /// </summary>
    public ClipboardEntry Insert(ClipboardEntry entry)
    {
        var saved = _store.Insert(entry);
        PurgeIfDue();
        return saved;
    }

    /// <summary>
    /// 删除三天前的记录，并删掉这些记录对应的图片文件。返回删除的行数。
    /// </summary>
    public int PurgeExpired()
    {
        _lastPurge = DateTime.UtcNow;
        var cutoff = RetentionCutoff();

        // 先按时间窗取出旧图片条目，数据库行删除前把文件清掉。
        var oldImages = _store.Query(new HistoryQuery(UntilTimestamp: cutoff, Type: "image", Limit: 0));
        foreach (var image in oldImages)
            DeleteEntryFiles(image);

        return _store.DeleteBefore(cutoff);
    }

    private void PurgeIfDue()
    {
        if (DateTime.UtcNow - _lastPurge < PurgeInterval)
            return;

        try
        {
            PurgeExpired();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"清理过期剪贴板记录失败: {ex.Message}");
            _lastPurge = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Delete a single entry by reference. Also removes its image file (if any).
    /// </summary>
    public bool Delete(ClipboardEntry entry)
    {
        if (entry.Id == 0) return false;
        DeleteEntryFiles(entry);
        var ok = _store.Delete(entry.Id);
        if (ok) Entries.Remove(entry);
        return ok;
    }

    /// <summary>
    /// Remove entries for the day expressed by <paramref name="mode"/>:
    /// "today" / "yesterday" / "daybefore"（前天）, or "all"。
    /// 因为历史只保留三天，按天清理即可覆盖全部数据。返回删除的行数。
    /// </summary>
    public int ClearByRange(string mode, string? type = null)
    {
        var (since, until) = ResolveRange(mode);

        // 只有清空全部或图片时才需要删图片文件；清空文本时不动磁盘。
        if (type != "text")
        {
            var images = _store.Query(new HistoryQuery(
                SinceTimestamp: since,
                UntilTimestamp: until,
                Type: "image",
                Limit: 0));
            foreach (var img in images)
                DeleteEntryFiles(img);
        }

        var removed = since is null || until is null
            ? _store.DeleteAll(type)
            : _store.DeleteRange(since, until, type);

        if (removed > 0)
        {
            // Refresh the visible window to reflect the deletion.
            ReloadView(GetCurrentQuery());
        }
        return removed;
    }

    /// <summary>把清空模式换算成 [since, until) 时间窗；"all" 返回两个 null。</summary>
    private static (string? Since, string? Until) ResolveRange(string mode)
    {
        var day = mode switch
        {
            "today" => DateTime.Today,
            "yesterday" => DateTime.Today.AddDays(-1),
            "daybefore" => DateTime.Today.AddDays(-2),
            _ => (DateTime?)null,
        };

        return day is null
            ? (null, null)
            : (day.Value.ToString("yyyy-MM-dd HH:mm:ss"),
               day.Value.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss"));
    }

    /// <summary>
    /// Returns the timestamp string for "today 00:00:00" in local time.
    /// </summary>
    public static string TodayStartTimestamp()
        => DateTime.Today.ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// Returns the timestamp string for "now minus N days" (used as a lower
    /// bound cutoff for "last week / last month" queries).
    /// </summary>
    public static string DaysAgoTimestamp(int days)
        => DateTime.Now.AddDays(-days).ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// Returns the number of rows that would be deleted by <see cref="ClearByRange"/> for
    /// the given mode. The visible window is irrelevant here — this counts the whole
    /// database so the confirmation message is accurate even when the user is looking
    /// at a narrow date range.
    /// </summary>
    public long CountByRange(string mode, string? type = null)
    {
        var (since, until) = ResolveRange(mode);
        return _store.Count(new HistoryQuery(
            SinceTimestamp: since,
            UntilTimestamp: until,
            Type: type));
    }

    public static void DeleteEntryFiles(ClipboardEntry entry)
    {
        if (!entry.IsImage || string.IsNullOrWhiteSpace(entry.ImagePath))
            return;

        var path = AppPaths.ResolveImagePath(entry.ImagePath);
        if (!File.Exists(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"删除图片失败: {ex.Message}");
        }
    }

    // -- Legacy migration --

    private void MigrateLegacyJson()
    {
        var path = AppPaths.HistoryFile;
        if (!File.Exists(path)) return;
        // If the DB already has data, leave the JSON file alone — the user
        // may want to inspect or recover it. It will simply be ignored.
        if (_store.Count() > 0) return;

        try
        {
            var json = File.ReadAllText(path);
            var list = JsonSerializer.Deserialize<List<ClipboardEntry>>(json, JsonOptions);
            if (list is null || list.Count == 0) return;

            // Insert in chronological order so dedup doesn't fight us; the
            // resulting timestamps will be the original ones, and re-copies
            // after migration will surface the correct entry at the top.
            foreach (var entry in list)
            {
                if (string.IsNullOrWhiteSpace(entry.Timestamp)) continue;
                _store.Insert(entry);
            }
            System.Diagnostics.Debug.WriteLine($"已从旧版 JSON 迁移 {list.Count} 条记录到 SQLite。");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"迁移历史记录失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns the current UI-visible query. The history service doesn't
    /// itself remember the filter, so this is a thin wrapper for callers
    /// (e.g. after a destructive operation) to use a default.
    /// </summary>
    public HistoryQuery GetCurrentQuery() => new(RetentionCutoff());

    /// <summary>保留窗口内的条数统计（总 / 文本 / 图片）。</summary>
    public ClipboardStatsView GetStats()
    {
        var counts = _store.CountByType(RetentionCutoff());
        var text = counts.GetValueOrDefault("text");
        var image = counts.GetValueOrDefault("image");
        return new ClipboardStatsView
        {
            Text = text,
            Image = image,
            Total = counts.Values.Sum(),
            RetentionDays = RetentionDays,
        };
    }

    /// <summary>三天前的时间戳，作为保留窗口的下界。</summary>
    public static string RetentionCutoff()
        => DateTime.Now.AddDays(-RetentionDays).ToString("yyyy-MM-dd HH:mm:ss");
}
