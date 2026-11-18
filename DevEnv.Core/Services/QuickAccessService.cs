using System.Runtime.InteropServices;

namespace DevEnv.Core.Services;

/// <summary>
/// 资源管理器「快速访问」固定文件夹的读取、固定与取消固定。
/// Shell.Application 的 Shell 命名空间要求 STA 线程：调用方若在后台线程（例如启动器数据加载），
/// 这里会自动切到专用 STA 线程执行；Shell 不可用或超时时按「未同步」降级，不影响启动器本身。
/// </summary>
public sealed class QuickAccessService
{
    /// <summary>快速访问的 shell 命名空间类标识。</summary>
    private const string QuickAccessNamespace = "shell:::{679f85cb-0220-4080-b29b-5540cc05aab6}";

    /// <summary>固定 / 取消固定后等待资源管理器刷新列表的最长时间。</summary>
    private static readonly TimeSpan VerifyTimeout = TimeSpan.FromMilliseconds(800);

    /// <summary>COM 调用整体超时；卡住时放弃本次同步，避免阻塞启动器。</summary>
    private static readonly TimeSpan ComTimeout = TimeSpan.FromSeconds(8);

    private static readonly TimeSpan VerifyInterval = TimeSpan.FromMilliseconds(80);

    /// <summary>
    /// 读取快速访问里固定的文件夹（按路径去重，不要求目录当前可访问）。
    /// Shell 不可用或读取失败时返回 false，调用方据此跳过本次对账，避免误删启动器条目。
    /// </summary>
    public bool TryGetPinnedFolders(out IReadOnlyList<string> folders)
    {
        IReadOnlyList<string>? result = null;
        var ok = TryRunOnSta(() =>
        {
            result = ReadPinnedCore();
            return true;
        });

        folders = result ?? Array.Empty<string>();
        return ok && result is not null;
    }

    /// <summary>把文件夹固定到快速访问；已固定或固定成功时返回 true。</summary>
    public bool Pin(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return false;

        var fullPath = ToFullPath(folderPath);
        return fullPath.Length > 0 && TryRunOnSta(() => PinCore(fullPath));
    }

    /// <summary>从快速访问取消固定；本来就不在快速访问里时同样返回 true。</summary>
    public bool Unpin(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return false;

        var fullPath = ToFullPath(folderPath);
        return fullPath.Length > 0 && TryRunOnSta(() => UnpinCore(fullPath));
    }

    private static bool PinCore(string fullPath)
    {
        if (Contains(ReadPinnedCore(), fullPath))
            return true;

        object? shell = null;
        object? folder = null;
        object? item = null;
        try
        {
            dynamic s = CreateShell();
            shell = s;

            dynamic? f = s.Namespace(fullPath);
            if (f is null)
                return false;
            folder = f;

            dynamic fi = f.Self;
            item = fi;
            fi.InvokeVerb("pintohome");

            // 资源管理器异步刷新快速访问，稍等片刻再确认；确认不到按失败处理，
            // 避免启动器把「没真正固定」的条目当成已同步而在下次同步时删掉。
            var deadline = DateTime.UtcNow + VerifyTimeout;
            do
            {
                Thread.Sleep(VerifyInterval);
                if (Contains(ReadPinnedCore(), fullPath))
                    return true;
            }
            while (DateTime.UtcNow < deadline);

            return false;
        }
        finally
        {
            Release(item);
            Release(folder);
            Release(shell);
        }
    }

    private static bool UnpinCore(string fullPath)
    {
        if (!Contains(ReadPinnedCore(), fullPath))
            return true;

        object? shell = null;
        object? ns = null;
        object? items = null;
        try
        {
            dynamic s = CreateShell();
            shell = s;

            dynamic? n = s.Namespace(QuickAccessNamespace);
            if (n is null)
                return false;
            ns = n;

            dynamic list = n.Items();
            items = list;
            var count = (int)list.Count;
            for (var i = 0; i < count; i++)
            {
                object? item = null;
                try
                {
                    dynamic entry = list.Item(i);
                    item = entry;
                    if (!IsFolder(entry))
                        continue;

                    var path = (string?)entry.Path;
                    if (string.IsNullOrWhiteSpace(path) || !SamePath(path, fullPath))
                        continue;

                    entry.InvokeVerb("unpinfromhome");

                    // 取消固定同样是异步的：确认列表里真的没了再返回，
                    // 否则紧随其后的同步会把还没刷新的条目又导入回来。
                    var deadline = DateTime.UtcNow + VerifyTimeout;
                    do
                    {
                        Thread.Sleep(VerifyInterval);
                        if (!Contains(ReadPinnedCore(), fullPath))
                            return true;
                    }
                    while (DateTime.UtcNow < deadline);

                    return false;
                }
                finally
                {
                    Release(item);
                }
            }

            // 列表里没找到 = 已经不在快速访问里
            return true;
        }
        finally
        {
            Release(items);
            Release(ns);
            Release(shell);
        }
    }

    private static List<string> ReadPinnedCore()
    {
        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        object? shell = null;
        object? ns = null;
        object? items = null;
        try
        {
            dynamic s = CreateShell();
            shell = s;

            dynamic? n = s.Namespace(QuickAccessNamespace);
            if (n is null)
                throw new InvalidOperationException("读不到资源管理器快速访问");
            ns = n;

            dynamic list = n.Items();
            items = list;
            var count = (int)list.Count;
            for (var i = 0; i < count; i++)
            {
                object? item = null;
                try
                {
                    dynamic entry = list.Item(i);
                    item = entry;
                    if (!IsFolder(entry))
                        continue;

                    var path = (string?)entry.Path;
                    if (string.IsNullOrWhiteSpace(path))
                        continue;

                    // 不要求目录当前可访问：离线的移动盘 / 网络盘仍算已固定，
                    // 不能因为暂时读不到就从启动器里删掉对应条目。
                    var normalized = Normalize(path);
                    if (normalized.Length == 0 || !seen.Add(normalized))
                        continue;

                    var fullPath = ToFullPath(path);
                    if (fullPath.Length > 0)
                        paths.Add(fullPath);
                }
                catch
                {
                    // 单个条目读失败不影响其余条目
                }
                finally
                {
                    Release(item);
                }
            }
        }
        finally
        {
            Release(items);
            Release(ns);
            Release(shell);
        }

        return paths;
    }

    private static dynamic CreateShell()
    {
        var type = Type.GetTypeFromProgID("Shell.Application")
            ?? throw new InvalidOperationException("Shell.Application 不可用");
        return Activator.CreateInstance(type, true)!;
    }

    private static bool IsFolder(dynamic item)
    {
        try
        {
            return (bool)item.IsFolder;
        }
        catch
        {
            return false;
        }
    }

    private static bool Contains(IReadOnlyList<string> paths, string fullPath)
    {
        foreach (var path in paths)
        {
            if (SamePath(path, fullPath))
                return true;
        }

        return false;
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string? path) =>
        path is null ? string.Empty : StartMenuIndex.NormalizePathForCompare(path);

    /// <summary>统一成完整路径并去掉尾部斜杠（盘符根目录如 <c>C:\</c> 保留）。</summary>
    private static string ToFullPath(string path)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Shell COM 的线程套间：调用线程已是 STA 时直接执行，否则起一个专用 STA 线程。</summary>
    private static bool TryRunOnSta(Func<bool> action)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            try
            {
                return action();
            }
            catch
            {
                return false;
            }
        }

        var result = false;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch
            {
                result = false;
            }
        })
        {
            IsBackground = true,
            Name = "DevEnv.QuickAccess",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return thread.Join(ComTimeout) && result;
    }

    private static void Release(object? com)
    {
        if (com is null)
            return;

        try
        {
            Marshal.FinalReleaseComObject(com);
        }
        catch
        {
            // COM 已释放或对象本就不是 RCW
        }
    }
}
