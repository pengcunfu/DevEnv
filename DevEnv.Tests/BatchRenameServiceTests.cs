using System.IO;
using DevEnv.Core.FileTools.Models;
using DevEnv.Core.FileTools.Services;

namespace DevEnv.Tests;

public sealed class BatchRenameServiceTests
{
    private readonly BatchRenameService _service = new();

    [Fact]
    public void Preview_AppliesPrefixSuffixAndNumbering()
    {
        using var directory = new BatchRenameTempDirectory();
        directory.Write("a.txt");
        directory.Write("b.txt");

        var preview = _service.Preview(directory.Path, new BatchRenameOptions
        {
            Prefix = "img_",
            Suffix = "_done",
            UseNumbering = true,
            StartNumber = 1,
            Step = 1,
            NumberWidth = 3,
            NumberSeparator = "-",
        });

        Assert.Equal(2, preview.TotalCount);
        Assert.Equal(2, preview.ChangedCount);
        Assert.Equal(0, preview.ErrorCount);
        Assert.Equal("img_a_done-001.txt", preview.Items[0].NewName);
        Assert.Equal("img_b_done-002.txt", preview.Items[1].NewName);
    }

    [Fact]
    public void Preview_FindReplaceAndCaseConversion()
    {
        using var directory = new BatchRenameTempDirectory();
        directory.Write("Report-2024.txt");

        var preview = _service.Preview(directory.Path, new BatchRenameOptions
        {
            Find = "Report",
            Replace = "Summary",
            CaseMode = RenameCaseMode.Lower,
        });

        Assert.Equal("summary-2024.txt", Assert.Single(preview.Items).NewName);
    }

    [Fact]
    public void Preview_DetectsDuplicateTargets()
    {
        using var directory = new BatchRenameTempDirectory();
        directory.Write("aa.txt");
        directory.Write("a.txt");

        // 两个文件去掉 "a" 后都变成 ".txt"
        var preview = _service.Preview(directory.Path, new BatchRenameOptions
        {
            Find = "a",
            Replace = string.Empty,
        });

        Assert.Equal(2, preview.ErrorCount);
        Assert.All(preview.Items, item => Assert.Contains("重复", item.Error, StringComparison.Ordinal));
    }

    [Fact]
    public void Preview_DetectsExistingTargetFile()
    {
        using var directory = new BatchRenameTempDirectory();
        directory.Write("a.txt");
        directory.Write("b.txt");

        var preview = _service.Preview(directory.Path, new BatchRenameOptions
        {
            Find = "a",
            Replace = "b",
        });

        var renamed = Assert.Single(preview.Items, item => item.OriginalName == "a.txt");
        Assert.Equal("目标文件已存在", renamed.Error);
    }

    [Fact]
    public void Preview_DetectsInvalidCharacters()
    {
        using var directory = new BatchRenameTempDirectory();
        directory.Write("a.txt");

        var preview = _service.Preview(directory.Path, new BatchRenameOptions { Suffix = ":" });

        var item = Assert.Single(preview.Items);
        Assert.NotNull(item.Error);
        Assert.Contains("非法字符", item.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_RenamesFilesOnDisk()
    {
        using var directory = new BatchRenameTempDirectory();
        directory.Write("a.txt", "A");
        directory.Write("b.txt", "B");

        var preview = _service.Preview(directory.Path, new BatchRenameOptions
        {
            Prefix = "new_",
            UseNumbering = true,
            NumberWidth = 1,
        });

        var result = _service.Apply(directory.Path, preview.Items);

        Assert.True(result.Ok);
        Assert.True(File.Exists(Path.Combine(directory.Path, "new_a_1.txt")));
        Assert.True(File.Exists(Path.Combine(directory.Path, "new_b_2.txt")));
        Assert.False(File.Exists(Path.Combine(directory.Path, "a.txt")));
    }

    [Fact]
    public void Apply_SupportsChainedRenames()
    {
        using var directory = new BatchRenameTempDirectory();
        directory.Write("a.txt");

        var items = new List<BatchRenameItem>
        {
            new(Path.Combine(directory.Path, "a.txt"), "a.txt", "b.txt", true, null),
        };

        var result = _service.Apply(directory.Path, items);

        Assert.True(result.Ok);
        Assert.True(File.Exists(Path.Combine(directory.Path, "b.txt")));
    }

    [Fact]
    public void Apply_DoesNotTouchUnchangedItems()
    {
        using var directory = new BatchRenameTempDirectory();
        directory.Write("a.txt");

        var preview = _service.Preview(directory.Path, new BatchRenameOptions());
        var result = _service.Apply(directory.Path, preview.Items);

        Assert.False(result.Ok);
        Assert.True(File.Exists(Path.Combine(directory.Path, "a.txt")));
    }
}

internal sealed class BatchRenameTempDirectory : IDisposable
{
    public BatchRenameTempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"devenv-rename-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Write(string relativePath, string content = "")
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
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
