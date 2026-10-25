using System.Diagnostics;
using System.Security.Cryptography;
using DevEnv.Core.FileTools.Models;

namespace DevEnv.Core.FileTools.Services;

/// <summary>
/// 文件粉碎：用随机数据覆盖文件内容若干遍，然后截断并删除。
/// 支持文件与文件夹（递归），单个文件失败不会中断整批任务。
/// </summary>
/// <remarks>
/// 覆盖只能保证写入逻辑扇区；SSD 的磨损均衡、TRIM、文件系统日志和云盘同步
/// 都可能在其他位置留下副本，因此粉碎不承诺物理层面的彻底擦除。
/// </remarks>
public sealed class FileShredService
{
    private const int BufferSize = 1024 * 1024;
    private const int MaxWarnings = 200;

    public async Task<ShredResult> ShredAsync(
        IReadOnlyList<string> paths,
        ShredOptions? options = null,
        IProgress<ShredProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ShredOptions();
        var passes = Math.Clamp(options.Passes, 1, 35);

        if (paths is null || paths.Count == 0)
            return new ShredResult { FatalError = "没有可粉碎的路径" };

        var files = new List<ShredFile>();
        var directories = new List<string>();
        var warnings = new List<string>();
        var initialFailures = new List<ShredFailure>();

        foreach (var raw in paths)
        {
            var error = ShredPathPolicy.Validate(raw);
            if (error is not null)
            {
                initialFailures.Add(new ShredFailure(raw ?? string.Empty, error));
                continue;
            }

            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(raw!));
            Collect(full, files, directories, warnings);
        }

        files = files
            .GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
        directories = directories
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(d => d.Length)
            .ToList();

        var result = new ShredResult { TotalFiles = files.Count };
        result.Failures.AddRange(initialFailures);
        result.Warnings.AddRange(warnings);

        var totalWork = files
            .Where(f => !f.IsLink)
            .Sum(f => WorkFor(f.Size, passes));

        long doneWork = 0;
        var stopwatch = Stopwatch.StartNew();

        void Report(string currentPath, int fileIndex, int pass, bool force)
        {
            if (progress is null)
                return;
            if (!force && stopwatch.ElapsedMilliseconds < 100)
                return;
            stopwatch.Restart();
            progress.Report(new ShredProgress
            {
                CurrentPath = currentPath,
                FileIndex = fileIndex,
                FileCount = files.Count,
                Pass = pass,
                Passes = passes,
                Percent = totalWork <= 0
                    ? 100
                    : Math.Min(100, doneWork * 100.0 / totalWork),
            });
        }

        Report(string.Empty, 0, 0, force: true);

        for (var index = 0; index < files.Count; index++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                result.Canceled = true;
                break;
            }

            var file = files[index];
            var fileIndex = index + 1;

            if (file.IsLink)
            {
                // 只删除链接本身，绝不沿链接覆盖目标文件。
                try
                {
                    File.Delete(file.Path);
                    result.ShreddedFiles++;
                }
                catch (Exception ex)
                {
                    result.Failures.Add(new ShredFailure(file.Path, DescribeError(ex)));
                }

                continue;
            }

            var startWork = doneWork;
            var fileWork = WorkFor(file.Size, passes);
            var overwritten = false;

            try
            {
                ClearReadOnly(file.Path);

                await using (var stream = new FileStream(
                                 file.Path,
                                 FileMode.Open,
                                 FileAccess.Write,
                                 FileShare.None,
                                 BufferSize,
                                 FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    var length = stream.Length;
                    var buffer = new byte[BufferSize];

                    for (var pass = 1; pass <= passes; pass++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        stream.Position = 0;
                        Report(file.Path, fileIndex, pass, force: true);

                        var remaining = length;
                        if (remaining == 0)
                        {
                            doneWork += 1; // 空文件也计入工作量，进度条才能走完
                            Report(file.Path, fileIndex, pass, force: false);
                        }

                        while (remaining > 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var chunk = (int)Math.Min(buffer.Length, remaining);
                            RandomNumberGenerator.Fill(buffer.AsSpan(0, chunk));
                            await stream.WriteAsync(buffer.AsMemory(0, chunk), cancellationToken)
                                .ConfigureAwait(false);
                            remaining -= chunk;
                            doneWork += chunk;
                            Report(file.Path, fileIndex, pass, force: false);
                        }

                        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                        stream.Flush(flushToDisk: true);
                    }

                    if (options.DeleteAfterShred)
                    {
                        stream.SetLength(0);
                        stream.Flush(flushToDisk: true);
                    }

                    result.OverwrittenBytes += length * passes;
                }

                overwritten = true;
            }
            catch (OperationCanceledException)
            {
                result.Canceled = true;
                break;
            }
            catch (Exception ex)
            {
                // 把该文件剩余工作量记完，避免进度条停在半截。
                doneWork = Math.Min(totalWork, startWork + fileWork);
                result.Failures.Add(new ShredFailure(file.Path, DescribeError(ex)));
                Report(file.Path, fileIndex, passes, force: true);
                continue;
            }

            if (!options.DeleteAfterShred)
            {
                result.ShreddedFiles++;
                continue;
            }

            try
            {
                DeleteFile(file.Path, options.RandomizeName);
                result.ShreddedFiles++;
            }
            catch (Exception ex)
            {
                var reason = overwritten
                    ? $"内容已覆盖，但删除失败：{ex.Message}"
                    : ex.Message;
                result.Failures.Add(new ShredFailure(file.Path, reason));
            }
        }

        if (!result.Canceled && options.DeleteAfterShred)
        {
            foreach (var directory in directories)
            {
                try
                {
                    Directory.Delete(directory, recursive: false);
                    result.RemovedDirectories++;
                }
                catch (DirectoryNotFoundException)
                {
                    // 已被删除或随文件一起消失
                }
                catch (Exception ex)
                {
                    if (result.Warnings.Count < MaxWarnings)
                        result.Warnings.Add($"目录未删除：{directory}（{ex.Message}）");
                }
            }
        }

        if (!result.Canceled)
        {
            doneWork = totalWork;
            Report(string.Empty, files.Count, passes, force: true);
        }

        return result;
    }

    private static long WorkFor(long size, int passes) => Math.Max(size, 1) * passes;

    private static void Collect(
        string root,
        List<ShredFile> files,
        List<string> directories,
        List<string> warnings)
    {
        var stack = new Stack<(string Path, bool Visited)>();
        stack.Push((root, false));

        while (stack.Count > 0)
        {
            var (path, visited) = stack.Pop();

            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(path);
            }
            catch (Exception ex)
            {
                AddWarning(warnings, $"无法读取：{path}（{ex.Message}）");
                continue;
            }

            if ((attributes & FileAttributes.Directory) != 0)
            {
                if (visited)
                {
                    directories.Add(path);
                    continue;
                }

                // 后序：先把目录压栈，再压子项；子项处理完才轮到目录本身。
                stack.Push((path, true));
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    continue;

                try
                {
                    foreach (var entry in Directory.GetFileSystemEntries(path))
                        stack.Push((entry, false));
                }
                catch (Exception ex)
                {
                    AddWarning(warnings, $"目录枚举不完整：{path}（{ex.Message}）");
                }

                continue;
            }

            var isLink = (attributes & FileAttributes.ReparsePoint) != 0;
            var size = 0L;
            if (!isLink)
            {
                try
                {
                    size = new FileInfo(path).Length;
                }
                catch (Exception ex)
                {
                    AddWarning(warnings, $"无法读取文件大小：{path}（{ex.Message}）");
                }
            }

            files.Add(new ShredFile(path, size, isLink));
        }
    }

    private static void AddWarning(List<string> warnings, string message)
    {
        if (warnings.Count < MaxWarnings)
            warnings.Add(message);
    }

    private static void ClearReadOnly(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) != 0)
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
    }

    private static void DeleteFile(string path, bool randomizeName)
    {
        if (randomizeName)
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    var randomName = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
                    var target = Path.Combine(directory, randomName + Path.GetExtension(path));
                    if (!File.Exists(target))
                    {
                        File.Move(path, target);
                        path = target;
                    }
                }
            }
            catch
            {
                // 改名失败不影响删除，继续用原路径。
            }
        }

        File.Delete(path);
    }

    private static string DescribeError(Exception ex) => ex switch
    {
        UnauthorizedAccessException => "权限不足，无法覆盖该文件",
        FileNotFoundException => "文件已不存在",
        DirectoryNotFoundException => "文件所在目录已不存在",
        IOException => $"无法覆盖（可能仍被占用）：{ex.Message}",
        _ => ex.Message,
    };

    private sealed record ShredFile(string Path, long Size, bool IsLink);
}
