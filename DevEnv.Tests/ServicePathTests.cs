using System.IO;
using DevEnv.Core.ServiceWatch.Models;

namespace DevEnv.Tests;

public class ServicePathTests
{
    [Fact]
    public void ExtractExecutable_处理带引号与参数()
    {
        Assert.Equal(@"C:\Program Files\Foo\foo.exe",
            ServicePath.ExtractExecutable(@"""C:\Program Files\Foo\foo.exe"" -service"));

        Assert.Equal(@"C:\Windows\system32\svchost.exe",
            ServicePath.ExtractExecutable(@"C:\Windows\system32\svchost.exe -k netsvcs -p"));
    }

    [Fact]
    public void ExtractExecutable_展开SystemRoot前缀()
    {
        var path = ServicePath.ExtractExecutable(@"\SystemRoot\System32\drivers\foo.sys");

        Assert.True(Path.IsPathRooted(path));
        Assert.EndsWith(@"System32\drivers\foo.sys", path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtractExecutable_去掉设备路径前缀()
    {
        Assert.Equal(@"C:\drivers\foo.sys",
            ServicePath.ExtractExecutable(@"\??\C:\drivers\foo.sys"));
    }

    [Fact]
    public void ExtractExecutable_展开环境变量()
    {
        var path = ServicePath.ExtractExecutable(@"%SystemRoot%\system32\svchost.exe -k netsvcs");

        Assert.True(Path.IsPathRooted(path));
        Assert.EndsWith(@"system32\svchost.exe", path, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractExecutable_空路径返回空串(string? imagePath) =>
        Assert.Equal("", ServicePath.ExtractExecutable(imagePath));

    [Fact]
    public void ExpandImagePath_保留引号与参数()
    {
        Assert.Equal(@"""C:\Program Files\Foo\foo.exe"" -k",
            ServicePath.ExpandImagePath(@"""C:\Program Files\Foo\foo.exe"" -k"));
    }
}
