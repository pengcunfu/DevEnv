using DevEnv.Core.FileTools.Models;
using DevEnv.Core.FileTools.Services;

namespace DevEnv.Tests;

public sealed class FileShredServiceTests
{
    private static readonly FileShredService Service = new();

    [Fact]
    public async Task ShredAsync_SingleFile_OverwritesAndDeletes()
    {
        using var dir = new TempDirectory();
        var file = dir.CreateFile("secret.bin", new string('A', 64 * 1024));

        var result = await Service.ShredAsync([file], new ShredOptions { Passes = 1 });

        Assert.True(result.Ok, string.Join("; ", result.Failures.Select(f => f.Error)));
        Assert.Equal(1, result.TotalFiles);
        Assert.Equal(1, result.ShreddedFiles);
        Assert.Equal(64 * 1024, result.OverwrittenBytes);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task ShredAsync_Directory_RecursesAndRemovesTree()
    {
        using var dir = new TempDirectory();
        var root = System.IO.Path.Combine(dir.Path, "nested");
        var first = System.IO.Path.Combine(root, "a.txt");
        var second = System.IO.Path.Combine(root, "sub", "b.txt");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(first)!);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(second)!);
        File.WriteAllText(first, "first");
        File.WriteAllText(second, "second");

        var result = await Service.ShredAsync([root], new ShredOptions { Passes = 1 });

        Assert.True(result.Ok, string.Join("; ", result.Failures.Select(f => f.Error)));
        Assert.Equal(2, result.TotalFiles);
        Assert.Equal(2, result.ShreddedFiles);
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task ShredAsync_OverwriteOnly_ChangesContentWithoutDeleting()
    {
        using var dir = new TempDirectory();
        var file = dir.CreateFile("keep.bin", new string('A', 4096));

        var result = await Service.ShredAsync(
            [file],
            new ShredOptions { Passes = 1, DeleteAfterShred = false, RandomizeName = false });

        Assert.True(result.Ok);
        Assert.True(File.Exists(file));
        var content = File.ReadAllBytes(file);
        Assert.Equal(4096, content.Length);
        Assert.Contains(content, b => b != (byte)'A');
    }

    [Fact]
    public async Task ShredAsync_ReadOnlyFile_ClearsAttributeAndDeletes()
    {
        using var dir = new TempDirectory();
        var file = dir.CreateFile("readonly.txt", "readonly");
        File.SetAttributes(file, FileAttributes.ReadOnly);

        var result = await Service.ShredAsync([file], new ShredOptions { Passes = 1 });

        Assert.True(result.Ok);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task ShredAsync_LockedFile_ReportsFailureAndKeepsContent()
    {
        using var dir = new TempDirectory();
        var file = dir.CreateFile("locked.txt", "locked");

        ShredResult result;
        using (var holder = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            result = await Service.ShredAsync([file], new ShredOptions { Passes = 1 });
        }

        Assert.False(result.Ok);
        Assert.Single(result.Failures);
        Assert.True(File.Exists(file));
        Assert.Equal("locked", File.ReadAllText(file));
    }

    [Fact]
    public async Task ShredAsync_EmptyFile_Deletes()
    {
        using var dir = new TempDirectory();
        var file = dir.CreateFile("empty.txt", string.Empty);

        var result = await Service.ShredAsync([file], new ShredOptions { Passes = 2 });

        Assert.True(result.Ok);
        Assert.False(File.Exists(file));
        Assert.Equal(1, result.ShreddedFiles);
    }

    [Fact]
    public async Task ShredAsync_MissingPath_ReportsFailureWithoutThrowing()
    {
        var missing = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"devenv-missing-{Guid.NewGuid():N}.txt");

        var result = await Service.ShredAsync([missing]);

        Assert.False(result.Ok);
        Assert.Null(result.FatalError);
        Assert.Single(result.Failures);
        Assert.Equal(0, result.TotalFiles);
    }

    [Fact]
    public async Task ShredAsync_PreCanceled_LeavesFileUntouched()
    {
        using var dir = new TempDirectory();
        var file = dir.CreateFile("keep.txt", "keep me");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await Service.ShredAsync([file], new ShredOptions { Passes = 1 }, cancellationToken: cts.Token);

        Assert.True(result.Canceled);
        Assert.True(File.Exists(file));
        Assert.Equal("keep me", File.ReadAllText(file));
    }

    [Fact]
    public async Task ShredAsync_ReportsProgressUpTo100()
    {
        using var dir = new TempDirectory();
        var file = dir.CreateFile("progress.bin", new string('B', 256 * 1024));
        var progress = new CollectingProgress();

        var result = await Service.ShredAsync([file], new ShredOptions { Passes = 2 }, progress);

        Assert.True(result.Ok);
        Assert.Contains(progress.Items, p => p.Percent > 0);
        Assert.Equal(100, progress.Items[^1].Percent, 3);
    }

    private sealed class CollectingProgress : IProgress<ShredProgress>
    {
        public List<ShredProgress> Items { get; } = [];

        public void Report(ShredProgress value)
        {
            lock (Items)
                Items.Add(value);
        }
    }
}

/// <summary>一次性临时目录，测试结束递归删除（含只读文件）。</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"devenv-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string CreateFile(string relativePath, string content)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        var directory = System.IO.Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            if (!Directory.Exists(Path))
                return;

            foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                catch
                {
                    // 清不掉属性也不影响后续尽力删除
                }
            }

            Directory.Delete(Path, recursive: true);
        }
        catch
        {
            // 临时目录清不掉不影响测试结论
        }
    }
}
