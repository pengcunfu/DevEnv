using DevEnv.Core.DiskCleaner.Models;
using DevEnv.Core.DiskCleaner.Services;

namespace DevEnv.Tests;

public sealed class DiskCleanupServiceTests
{
    private static CleanupTargetDefinition Target(string root, string? pattern = null) => new()
    {
        Id = "test-target",
        Name = "测试清理项",
        Category = "临时文件",
        Locations = [new CleanupLocation { Path = root, SearchPattern = pattern }],
    };

    [Fact]
    public void GetTargets_AllLocationsPassSafetyPolicy()
    {
        var service = new DiskCleanupService();

        var targets = service.GetTargets();

        Assert.Contains(targets, t => t.Id == "user-temp");
        Assert.Contains(targets, t => t.Id == "recycle-bin");
        Assert.Equal(targets.Count, targets.Select(t => t.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var location in targets.SelectMany(t => t.Locations))
            Assert.Null(CleanupPathPolicy.ValidateRoot(location.Path));
    }

    [Fact]
    public void Scan_CountsFilesAndBytesRecursively()
    {
        using var directory = new CleanupTempDirectory();
        directory.Write("a.txt", "12345");
        directory.Write("sub\\b.txt", "1234567890");

        var service = new DiskCleanupService();
        var scan = service.Scan([Target(directory.Path)]);

        var item = Assert.Single(scan.Items);
        Assert.True(item.Exists);
        Assert.True(item.Accessible);
        Assert.Equal(2, item.FileCount);
        Assert.Equal(15, item.SizeBytes);
        Assert.Equal(15, scan.TotalBytes);
        Assert.Equal(2, scan.TotalFiles);
    }

    [Fact]
    public void Scan_MissingDirectory_ReportsNotExists()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"devenv-missing-{Guid.NewGuid():N}");

        var service = new DiskCleanupService();
        var scan = service.Scan([Target(missing)]);

        var item = Assert.Single(scan.Items);
        Assert.False(item.Exists);
        Assert.Equal(0, item.SizeBytes);
    }

    [Fact]
    public void Scan_SearchPattern_OnlyCountsMatchingFiles()
    {
        using var directory = new CleanupTempDirectory();
        directory.Write("thumbcache_1.db", "abc");
        directory.Write("other.txt", "12345");

        var service = new DiskCleanupService();
        var scan = service.Scan([Target(directory.Path, "thumbcache_*.db")]);

        var item = Assert.Single(scan.Items);
        Assert.Equal(1, item.FileCount);
        Assert.Equal(3, item.SizeBytes);
    }

    [Fact]
    public void Clean_DeletesContentsButKeepsRootDirectory()
    {
        using var directory = new CleanupTempDirectory();
        directory.Write("a.txt", "12345");
        directory.Write("sub\\b.txt", "1234567890");

        var service = new DiskCleanupService();
        var scan = service.Scan([Target(directory.Path)]);
        var result = service.Clean([Target(directory.Path)], scan.TotalBytes, scan.TotalFiles);

        Assert.False(result.Canceled);
        Assert.Equal(2, result.TotalDeletedFiles);
        Assert.Equal(15, result.TotalFreedBytes);
        Assert.True(Directory.Exists(directory.Path));
        Assert.Empty(Directory.GetFiles(directory.Path, "*", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetDirectories(directory.Path));
    }

    [Fact]
    public void Clean_SearchPattern_LeavesOtherFiles()
    {
        using var directory = new CleanupTempDirectory();
        directory.Write("thumbcache_1.db", "abc");
        directory.Write("other.txt", "12345");

        var service = new DiskCleanupService();
        var result = service.Clean([Target(directory.Path, "thumbcache_*.db")], 3, 1);

        Assert.Equal(1, result.TotalDeletedFiles);
        Assert.Equal(3, result.TotalFreedBytes);
        Assert.False(File.Exists(Path.Combine(directory.Path, "thumbcache_1.db")));
        Assert.True(File.Exists(Path.Combine(directory.Path, "other.txt")));
    }

    [Fact]
    public void Clean_CanceledToken_StopsWithoutDeleting()
    {
        using var directory = new CleanupTempDirectory();
        var file = directory.Write("a.txt", "12345");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var service = new DiskCleanupService();
        var result = service.Clean([Target(directory.Path)], 5, 1, cancellationToken: cts.Token);

        Assert.True(result.Canceled);
        Assert.Equal(0, result.TotalDeletedFiles);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void Clean_RejectsProtectedRoot()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrWhiteSpace(windows))
            return;

        var service = new DiskCleanupService();
        var result = service.Clean([Target(windows)], 0, 0);

        var outcome = Assert.Single(result.Outcomes);
        Assert.Equal(0, outcome.DeletedFiles);
        Assert.Equal(0, outcome.FreedBytes);
    }

    [Fact]
    public void Clean_DoesNotDeleteRoot_WhenOnlyEmptyDirectory()
    {
        using var directory = new CleanupTempDirectory();

        var service = new DiskCleanupService();
        var result = service.Clean([Target(directory.Path)], 0, 0);

        Assert.Equal(0, result.TotalDeletedFiles);
        Assert.True(Directory.Exists(directory.Path));
    }
}

/// <summary>测试用的唯一临时目录，清理失败不影响测试结论。</summary>
internal sealed class CleanupTempDirectory : IDisposable
{
    public CleanupTempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"devenv-clean-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Write(string relativePath, string content)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        var parent = System.IO.Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
        catch
        {
            // 临时目录清不掉不影响测试结论
        }
    }
}
