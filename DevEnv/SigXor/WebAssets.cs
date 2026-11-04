using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace DevEnv.Host.SigXor;

/// <summary>
/// 随程序嵌入的离线网页资源（webassets）。
/// WebView2 的虚拟主机映射只接受真实目录，故首次使用时把嵌入资源解压到
/// %LOCALAPPDATA%\SigXor\WebAssets\{内容指纹}；资源未变化时同一目录直接复用。
/// </summary>
internal static class WebAssets
{
    private const string ResourcePrefix = "webassets/";
    private const string ReadyMarker = ".ready";

    private static readonly Lazy<string> RootLazy =
        new(ResolveRoot, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>已解压的 webassets 目录（首次访问时解压）。</summary>
    public static string Root => RootLazy.Value;

    /// <summary>webassets 内某个文件的完整路径。</summary>
    public static string GetPath(string fileName) => Path.Combine(Root, fileName);

    private static string ResolveRoot()
    {
        Assembly assembly = typeof(WebAssets).Assembly;
        List<string> names = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        if (names.Count == 0)
            throw new InvalidOperationException("程序未包含 webassets 嵌入资源");

        string cacheParent = Path.Combine(
            GetCacheRoot(), "SigXor", "WebAssets");
        string root = Path.Combine(cacheParent, ContentKey(assembly, names));
        if (File.Exists(Path.Combine(root, ReadyMarker)))
            return root;

        Directory.CreateDirectory(cacheParent);
        string staging = root + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            ExtractTo(assembly, names, staging);
            File.WriteAllText(Path.Combine(staging, ReadyMarker), "");
            if (Directory.Exists(root))
                TryDeleteDirectory(root); // 上次解压中断的残留
            try
            {
                Directory.Move(staging, root);
            }
            catch (IOException) when (File.Exists(Path.Combine(root, ReadyMarker)))
            {
                // 另一个实例抢先完成了解压，直接用它的结果
                TryDeleteDirectory(staging);
            }
        }
        catch
        {
            TryDeleteDirectory(staging);
            throw;
        }

        PruneOldVersions(cacheParent, Path.GetFileName(root));
        return root;
    }

    private static string GetCacheRoot()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrEmpty(local) ? Path.GetTempPath() : local;
    }

    /// <summary>资源名 + 内容哈希，作为缓存目录名：构建一变就换目录。</summary>
    private static string ContentKey(Assembly assembly, IReadOnlyList<string> names)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[81920];
        foreach (string name in names)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(name));
            using Stream stream = assembly.GetManifestResourceStream(name)!;
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                hash.AppendData(buffer, 0, read);
        }
        return Convert.ToHexString(hash.GetHashAndReset())[..16].ToLowerInvariant();
    }

    private static void ExtractTo(Assembly assembly, IReadOnlyList<string> names, string destination)
    {
        foreach (string name in names)
        {
            string relative = name[ResourcePrefix.Length..].Replace('\\', '/');
            if (relative.Split('/').Any(segment => segment is "" or "." or ".."))
                throw new InvalidOperationException($"非法 webassets 资源名: {name}");

            string path = Path.Combine(
                destination, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using Stream source = assembly.GetManifestResourceStream(name)!;
            using FileStream target = File.Create(path);
            source.CopyTo(target);
        }
    }

    /// <summary>清理旧构建留下的缓存目录（一小时内的目录可能属于并行实例，保留）。</summary>
    private static void PruneOldVersions(string cacheParent, string keep)
    {
        try
        {
            DateTime threshold = DateTime.UtcNow.AddHours(-1);
            foreach (string dir in Directory.EnumerateDirectories(cacheParent))
            {
                if (string.Equals(Path.GetFileName(dir), keep, StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    if (Directory.GetLastWriteTimeUtc(dir) < threshold)
                        TryDeleteDirectory(dir);
                }
                catch
                {
                    // 目录不可访问时忽略，下次启动再试
                }
            }
        }
        catch
        {
            // 清理失败不影响预览
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch
        {
            // 被占用（另一实例正在用）时保留
        }
    }
}
