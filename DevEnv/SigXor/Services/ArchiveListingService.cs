using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using DevEnv.Core.SigXor;

namespace DevEnv.Host.SigXor.Services;

public sealed record ArchiveEntryInfo(
    string Path,
    bool IsDirectory,
    long? Size,
    long? CompressedSize,
    DateTime? Modified);

public sealed record ArchiveListing(
    IReadOnlyList<ArchiveEntryInfo> Entries,
    int FileCount,
    int DirectoryCount,
    long? TotalUncompressed,
    bool Truncated,
    string? Notice);

/// <summary>
/// 压缩包目录列表：仅用 BCL（ZipArchive / TarReader / GZipStream），不引入 SharpCompress 等重型库。
/// zip/jar/nupkg、tar、tar.gz/tgz、纯 gz 可列目录；7z/rar/bz2/xz 给出明确提示。
/// </summary>
public static class ArchiveListingService
{
    public const int MaxEntries = 5_000;

    public static ArchiveListing List(string path)
    {
        try
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            string name = Path.GetFileName(path).ToLowerInvariant();

            if (ext is ".zip" or ".jar" or ".nupkg")
                return FromZip(path);

            if (ext is ".tar")
                return FromTar(path, gzip: false);

            if (ext is ".tgz" || name.EndsWith(".tar.gz", StringComparison.Ordinal))
                return FromTar(path, gzip: true);

            if (ext is ".gz" && !name.EndsWith(".tar.gz", StringComparison.Ordinal))
                return FromGzipSingle(path);

            if (ext is ".7z" or ".rar" or ".bz2" or ".xz")
            {
                return new ArchiveListing(
                    [], 0, 0, null, false,
                    $"{ext.TrimStart('.')} 目录列表需专用解压库。当前仅支持 ZIP / TAR / TAR.GZ（轻量 BCL），不解压内容。");
            }

            // 其它扩展名：先试 ZIP，再试 TAR
            try { return FromZip(path); }
            catch
            {
                try { return FromTar(path, gzip: false); }
                catch (Exception ex)
                {
                    return new ArchiveListing([], 0, 0, null, false, $"无法读取压缩包：{ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            return new ArchiveListing([], 0, 0, null, false, $"无法读取压缩包：{ex.Message}");
        }
    }

    private static ArchiveListing FromZip(string path)
    {
        using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);
        var list = new List<ArchiveEntryInfo>();
        int files = 0, dirs = 0;
        long total = 0;
        bool truncated = false;

        foreach (var entry in zip.Entries)
        {
            if (list.Count >= MaxEntries)
            {
                truncated = true;
                break;
            }

            string key = NormalizePath(entry.FullName);
            if (string.IsNullOrEmpty(key)) continue;

            bool isDir = entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\') || entry.Name.Length == 0;
            if (isDir)
            {
                dirs++;
                list.Add(new ArchiveEntryInfo(key, true, null, null, entry.LastWriteTime.DateTime));
            }
            else
            {
                files++;
                total += entry.Length;
                list.Add(new ArchiveEntryInfo(
                    key, false, entry.Length, entry.CompressedLength, entry.LastWriteTime.DateTime));
            }
        }

        list.Sort(CompareEntries);
        return Finish(list, files, dirs, total, truncated);
    }

    private static ArchiveListing FromTar(string path, bool gzip)
    {
        using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (gzip)
        {
            using var gz = new GZipStream(fs, CompressionMode.Decompress, leaveOpen: true);
            return ReadTarEntries(gz);
        }
        return ReadTarEntries(fs);
    }

    private static ArchiveListing ReadTarEntries(Stream stream)
    {
        using var reader = new TarReader(stream, leaveOpen: true);
        var list = new List<ArchiveEntryInfo>();
        int files = 0, dirs = 0;
        long total = 0;
        bool truncated = false;

        TarEntry? entry;
        while ((entry = reader.GetNextEntry(copyData: false)) is not null)
        {
            if (list.Count >= MaxEntries)
            {
                truncated = true;
                break;
            }

            string key = NormalizePath(entry.Name);
            if (string.IsNullOrEmpty(key)) continue;

            bool isDir = entry.EntryType is TarEntryType.Directory
                         || entry.Name.EndsWith('/') || entry.Name.EndsWith('\\');
            if (isDir)
            {
                dirs++;
                list.Add(new ArchiveEntryInfo(key, true, null, null, entry.ModificationTime.UtcDateTime));
            }
            else
            {
                files++;
                long size = entry.Length;
                if (size > 0) total += size;
                list.Add(new ArchiveEntryInfo(
                    key, false, size >= 0 ? size : null, null, entry.ModificationTime.UtcDateTime));
            }
        }

        list.Sort(CompareEntries);
        return Finish(list, files, dirs, total, truncated);
    }

    private static ArchiveListing FromGzipSingle(string path)
    {
        var fi = new FileInfo(path);
        string inner = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrEmpty(inner)) inner = Path.GetFileName(path);
        var entry = new ArchiveEntryInfo(inner, false, null, fi.Length, fi.LastWriteTime);
        return new ArchiveListing(
            [entry], 1, 0, null, false,
            "纯 GZip 单文件：仅显示压缩后大小，不解压内容。");
    }

    private static ArchiveListing Finish(
        List<ArchiveEntryInfo> list, int files, int dirs, long total, bool truncated)
    {
        string? notice = truncated ? $"仅显示前 {MaxEntries:N0} 项" : null;
        return new ArchiveListing(list, files, dirs, total > 0 ? total : null, truncated, notice);
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').Trim('/');

    private static int CompareEntries(ArchiveEntryInfo a, ArchiveEntryInfo b)
    {
        if (a.IsDirectory != b.IsDirectory) return a.IsDirectory ? -1 : 1;
        return string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatEntrySize(long? size) =>
        size is null ? "" : FileEntry.FormatSize(size.Value);
}
