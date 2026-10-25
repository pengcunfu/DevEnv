using DevEnv.Core.FileTools.Services;

namespace DevEnv.Tests;

public sealed class ShredPathPolicyTests
{
    [Fact]
    public void Validate_NormalTempFile_Allows()
    {
        using var file = TempFile.Create("policy-ok.txt", "x");

        Assert.Null(ShredPathPolicy.Validate(file.Path));
    }

    [Fact]
    public void Validate_MissingPath_Rejects()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"devenv-missing-{Guid.NewGuid():N}.txt");

        Assert.Equal("路径不存在", ShredPathPolicy.Validate(missing));
    }

    [Fact]
    public void Validate_RelativePath_Rejects()
    {
        Assert.Contains("绝对路径", ShredPathPolicy.Validate("some-file.txt"));
    }

    [Fact]
    public void Validate_EmptyPath_Rejects()
    {
        Assert.False(string.IsNullOrEmpty(ShredPathPolicy.Validate("  ")));
        Assert.False(string.IsNullOrEmpty(ShredPathPolicy.Validate(null)));
    }

    [Fact]
    public void Validate_DriveRoot_Rejects()
    {
        var root = Path.GetPathRoot(Path.GetTempPath());
        Assert.NotNull(root);
        Assert.Contains("根目录", ShredPathPolicy.Validate(root));
    }

    [Fact]
    public void Validate_WindowsDirectory_Rejects()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrWhiteSpace(windows))
            return;

        Assert.Contains("系统关键目录", ShredPathPolicy.Validate(windows));
    }

    [Fact]
    public void Validate_AppBaseDirectory_Rejects()
    {
        Assert.Contains("当前程序", ShredPathPolicy.Validate(AppContext.BaseDirectory));
    }

    [Fact]
    public void IsSameOrUnder_MatchesSelfAndChildren()
    {
        var root = Path.Combine(Path.GetTempPath(), "devenv-policy-root");

        Assert.True(ShredPathPolicy.IsSameOrUnder(root, root));
        Assert.True(ShredPathPolicy.IsSameOrUnder(Path.Combine(root, "a", "b.txt"), root));
        Assert.False(ShredPathPolicy.IsSameOrUnder(Path.Combine(Path.GetTempPath(), "devenv-other"), root));
    }

    [Fact]
    public void IsDriveRoot_DetectsOnlyDriveRoots()
    {
        var root = Path.GetPathRoot(Path.GetTempPath());
        Assert.NotNull(root);
        Assert.True(ShredPathPolicy.IsDriveRoot(root));
        Assert.False(ShredPathPolicy.IsDriveRoot(Path.GetTempPath()));
    }
}
