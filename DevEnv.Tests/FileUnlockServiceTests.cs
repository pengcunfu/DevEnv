using DevEnv.Core.FileTools.Services;

namespace DevEnv.Tests;

public sealed class FileUnlockServiceTests
{
    [Fact]
    public void Scan_ExclusiveOpen_FindsCurrentProcess()
    {
        using var file = TempFile.Create("lock-probe.txt", "x");
        using var stream = new FileStream(file.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var (result, scan) = new FileUnlockService().Scan(file.Path);

        Assert.True(result.Ok, result.DisplayMessage);
        Assert.NotNull(scan);
        Assert.Contains(scan.Processes, p => p.ProcessId == Environment.ProcessId);
    }

    [Fact]
    public void Scan_Directory_ExpandsFilesAndFindsLock()
    {
        using var dir = new TempDirectory();
        var file = dir.CreateFile("nested/locked.txt", "x");
        using var stream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var (result, scan) = new FileUnlockService().Scan(dir.Path);

        Assert.True(result.Ok, result.DisplayMessage);
        Assert.NotNull(scan);
        Assert.Equal(1, scan.RegisteredFileCount);
        Assert.Contains(scan.Processes, p => p.ProcessId == Environment.ProcessId);
    }

    [Fact]
    public void Scan_MissingPath_Fails()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"devenv-missing-{Guid.NewGuid():N}.txt");

        var (result, scan) = new FileUnlockService().Scan(missing);

        Assert.False(result.Ok);
        Assert.Null(scan);
    }
}
