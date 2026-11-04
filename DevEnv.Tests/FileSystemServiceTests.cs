using DevEnv.Core.SigXor;

namespace DevEnv.Tests.SigXor;

public sealed class FileSystemServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "SigXorFsTests-" + Guid.NewGuid().ToString("N"));

    public FileSystemServiceTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public void InspectDirectory_Sums_Nested_Files_And_Counts()
    {
        string nested = Path.Combine(_dir, "a", "b");
        Directory.CreateDirectory(nested);
        File.WriteAllBytes(Path.Combine(_dir, "root.bin"), new byte[100]);
        File.WriteAllBytes(Path.Combine(_dir, "a", "mid.bin"), new byte[50]);
        File.WriteAllBytes(Path.Combine(nested, "leaf.bin"), new byte[25]);

        DirectoryStats stats = FileSystemService.InspectDirectory(_dir);

        Assert.Equal(175, stats.TotalBytes);
        Assert.Equal(3, stats.FileCount);
        Assert.Equal(2, stats.DirectoryCount);
        Assert.Equal(0, stats.Skipped);
    }

    [Fact]
    public void InspectDirectory_Empty_Folder_Is_Zero()
    {
        DirectoryStats stats = FileSystemService.InspectDirectory(_dir);
        Assert.Equal(0, stats.TotalBytes);
        Assert.Equal(0, stats.FileCount);
        Assert.Equal(0, stats.DirectoryCount);
    }
}
