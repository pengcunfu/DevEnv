namespace DevEnv.Core.Services;

/// <summary>一次快速访问对账的结果；<see cref="Failed"/> 表示没读到系统列表，本次不改数据。</summary>
public sealed record QuickAccessSyncResult(bool Succeeded, int Imported, int Removed, int MarkedPinned)
{
    public static QuickAccessSyncResult Failed { get; } = new(false, 0, 0, 0);

    /// <summary>本次是否写过 data.json。</summary>
    public bool Changed => Imported > 0 || Removed > 0 || MarkedPinned > 0;
}

/// <summary>启动器文件夹与快速访问固定项的对账计划，索引基于传入的启动器列表。</summary>
public sealed record QuickAccessPlan(
    IReadOnlyList<string> ImportPaths,
    IReadOnlyList<int> RemoveIndexes,
    IReadOnlyList<int> MarkPinnedIndexes);

/// <summary>
/// 启动器「文件夹」页与资源管理器快速访问的对账（纯逻辑，便于单测）：
/// 快速访问有、启动器没有的导入；两边都有但未标记的补标记；
/// 启动器标记为已固定、快速访问里却没有的移除（用户在资源管理器里取消了固定）。
/// 没有标记的手动条目不受快速访问增删影响。
/// </summary>
public static class QuickAccessReconciler
{
    public static QuickAccessPlan Build(
        IReadOnlyList<(string Path, bool Pinned)> launcherFolders,
        IReadOnlyList<string> quickAccessFolders)
    {
        var quickKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var launcherKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (path, _) in launcherFolders)
        {
            var key = StartMenuIndex.NormalizePathForCompare(path);
            if (key.Length > 0)
                launcherKeys.Add(key);
        }

        var imports = new List<string>();
        foreach (var path in quickAccessFolders)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            var key = StartMenuIndex.NormalizePathForCompare(path);
            if (key.Length == 0 || !quickKeys.Add(key))
                continue;

            if (!launcherKeys.Contains(key))
                imports.Add(Path.TrimEndingDirectorySeparator(path.Trim()));
        }

        var removes = new List<int>();
        var marks = new List<int>();
        for (var i = 0; i < launcherFolders.Count; i++)
        {
            var (path, pinned) = launcherFolders[i];
            var key = StartMenuIndex.NormalizePathForCompare(path);
            if (key.Length == 0)
                continue;

            if (quickKeys.Contains(key))
            {
                if (!pinned)
                    marks.Add(i);
            }
            else if (pinned)
            {
                removes.Add(i);
            }
        }

        return new QuickAccessPlan(imports, removes, marks);
    }
}
