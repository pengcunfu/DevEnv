using System.Security.Cryptography;
using System.Text;
using DevEnv.Core.LavaEnv.Services;

namespace DevEnv.Tests;

public sealed class HashCalculatorServiceTests
{
    private static readonly HashCalculatorService Service = new();

    [Fact]
    public async Task CalculateTextHashAsync_MatchesKnownDigest()
    {
        var md5 = await Service.CalculateTextHashAsync("hello", HashType.MD5);
        Assert.Equal("5D41402ABC4B2A76B9719D911017C592", md5.Hash);
        Assert.Equal("MD5", md5.Algorithm);
        Assert.Equal(Encoding.UTF8.GetByteCount("hello"), md5.FileSize);

        var sha256 = await Service.CalculateTextHashAsync("abc", HashType.SHA256);
        Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", sha256.Hash);
    }

    [Fact]
    public async Task CalculateTextHashAsync_SameInput_GivesSameDigest()
    {
        var first = await Service.CalculateTextHashAsync("DevEnv", HashType.SHA512);
        var second = await Service.CalculateTextHashAsync("DevEnv", HashType.SHA512);

        Assert.Equal(first.Hash, second.Hash);
        Assert.Equal(64 * 2, first.Hash.Length);
    }

    [Fact]
    public async Task CalculateTextHashAsync_Empty_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service.CalculateTextHashAsync("", HashType.MD5));
    }

    [Fact]
    public void FormatFileSize_UsesTwoDecimals()
    {
        using var _ = new InvariantCultureScope();

        Assert.Equal("0.00 B", HashCalculatorService.FormatFileSize(0));
        Assert.Equal("1.00 KB", HashCalculatorService.FormatFileSize(1024));
        Assert.Equal("1.50 KB", HashCalculatorService.FormatFileSize(1536));
        Assert.Equal("1.00 MB", HashCalculatorService.FormatFileSize(1024 * 1024));
    }

    [Fact]
    public void IsValidFile_OnlyAcceptsExistingNonEmptyPath()
    {
        using var file = TempFile.Create("hash-input.txt");

        Assert.True(HashCalculatorService.IsValidFile(file.Path));
        Assert.False(HashCalculatorService.IsValidFile(""));
        Assert.False(HashCalculatorService.IsValidFile(Path.Combine(Path.GetTempPath(), "devenv-missing-file.txt")));
    }

    [Fact]
    public async Task CalculateFileHashAsync_ReadsFromDisk()
    {
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("hello")));

        using var file = TempFile.Create("hash-file.txt", "hello");
        var result = await Service.CalculateFileHashAsync(file.Path, HashType.SHA256);

        Assert.Equal(expected, result.Hash);
        Assert.Equal(5, result.FileSize);
    }

    [Fact]
    public async Task CalculateFileHashAsync_MissingFile_Throws()
    {
        var missing = Path.Combine(Path.GetTempPath(), "devenv-missing-hash.txt");
        await Assert.ThrowsAsync<FileNotFoundException>(() => Service.CalculateFileHashAsync(missing, HashType.MD5));
    }
}

/// <summary>一次性临时文件，测试结束自动删除。</summary>
internal sealed class TempFile : IDisposable
{
    private TempFile(string path) => Path = path;

    public string Path { get; }

    public static TempFile Create(string name, string content = "")
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"devenv-test-{Guid.NewGuid():N}-{name}");
        File.WriteAllText(path, content);
        return new TempFile(path);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(Path))
                File.Delete(Path);
        }
        catch
        {
            // 临时文件清不掉不影响测试结论
        }
    }
}