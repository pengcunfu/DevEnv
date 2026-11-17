using DevEnv.Core.DiskCleaner.Services;

namespace DevEnv.Tests;

public sealed class CleanupPathPolicyTests
{
    [Fact]
    public void ValidateRoot_DriveRoot_Rejects()
    {
        var root = Path.GetPathRoot(Path.GetTempPath());
        Assert.NotNull(root);
        Assert.Contains("根目录", CleanupPathPolicy.ValidateRoot(root));
    }

    [Fact]
    public void ValidateRoot_WindowsDirectory_Rejects()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrWhiteSpace(windows))
            return;

        Assert.Contains("系统关键目录", CleanupPathPolicy.ValidateRoot(windows));
    }

    [Fact]
    public void ValidateRoot_UserProfile_Rejects()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(profile))
            return;

        Assert.Contains("系统关键目录", CleanupPathPolicy.ValidateRoot(profile));
    }

    [Fact]
    public void ValidateRoot_AppBaseDirectory_Rejects()
    {
        Assert.Contains("当前程序", CleanupPathPolicy.ValidateRoot(AppContext.BaseDirectory));
    }

    [Fact]
    public void ValidateRoot_TempSubdirectory_Allows()
    {
        using var directory = new CleanupTempDirectory();

        Assert.Null(CleanupPathPolicy.ValidateRoot(directory.Path));
    }

    [Fact]
    public void ValidateRoot_RelativePath_Rejects()
    {
        Assert.Contains("绝对路径", CleanupPathPolicy.ValidateRoot("temp"));
    }

    [Fact]
    public void ValidateRoot_EmptyPath_Rejects()
    {
        Assert.NotNull(CleanupPathPolicy.ValidateRoot(" "));
        Assert.NotNull(CleanupPathPolicy.ValidateRoot(null));
    }

    [Fact]
    public void IsSameOrUnder_HandlesBoundariesAndCase()
    {
        var root = Path.Combine(Path.GetTempPath(), "devenv-scope");

        Assert.True(CleanupPathPolicy.IsSameOrUnder(root, root));
        Assert.True(CleanupPathPolicy.IsSameOrUnder(Path.Combine(root, "a.txt"), root));
        Assert.True(CleanupPathPolicy.IsSameOrUnder(
            Path.Combine(root, "sub", "a.txt").ToUpperInvariant(), root));
        Assert.False(CleanupPathPolicy.IsSameOrUnder(root + "2", root));
    }

    [Fact]
    public void ShouldSkipEntry_SkipsApplicationDirectory()
    {
        var appDirectory = AppContext.BaseDirectory;
        var child = Path.Combine(appDirectory, "some-file.txt");

        Assert.True(CleanupPathPolicy.ShouldSkipEntry(appDirectory));
        Assert.True(CleanupPathPolicy.ShouldSkipEntry(child));
    }

    [Fact]
    public void ShouldSkipEntry_AllowsOrdinaryTempEntry()
    {
        using var directory = new CleanupTempDirectory();
        var file = directory.Write("temp-file.txt", "x");

        Assert.False(CleanupPathPolicy.ShouldSkipEntry(file));
    }

    [Fact]
    public void IsReparsePoint_NormalDirectory_ReturnsFalse()
    {
        using var directory = new CleanupTempDirectory();

        Assert.False(CleanupPathPolicy.IsReparsePoint(directory.Path));
    }
}
