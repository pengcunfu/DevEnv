using DevEnv.Core.Models;
using DevEnv.Core.Services;

namespace DevEnv.Tests;

public sealed class DevServiceProcessResolverTests
{
    [Theory]
    [InlineData("redis-server.exe", "redis-server")]
    [InlineData("redis-server.exe redis.windows.conf", "redis-server")]
    [InlineData(@"bin\postgres -D data", "postgres")]
    [InlineData(@"bin\mysqld", "mysqld")]
    [InlineData("routatic-proxy start", "routatic-proxy")]
    [InlineData(@".\redis-server.exe", "redis-server")]
    [InlineData("  redis-server.exe  ", "redis-server")]
    [InlineData(@"""C:\Program Files\PostgreSQL\bin\postgres.exe"" -D data", "postgres")]
    public void ExtractExecutableName_DirectExecutables(string command, string expected)
    {
        Assert.Equal(expected, DevServiceProcessResolver.ExtractExecutableName(command));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("npm run dev")]
    [InlineData("node server.js")]
    [InlineData("python -m http.server")]
    [InlineData("java -jar kafka.jar")]
    [InlineData("run.bat")]
    [InlineData("start-server.cmd")]
    [InlineData(@"""C:\tools\start.cmd""")]
    [InlineData("start \"\" redis-server.exe")]
    public void ExtractExecutableName_SharedHostsAndScripts_ReturnEmpty(string command)
    {
        Assert.Equal(string.Empty, DevServiceProcessResolver.ExtractExecutableName(command));
    }

    [Fact]
    public void ResolveProcessName_ExplicitNameWins()
    {
        var config = new DevServiceConfig
        {
            StartCommand = "redis-server.exe",
            ProcessName = " node ",
        };

        Assert.Equal("node", DevServiceProcessResolver.ResolveProcessName(config));
        Assert.True(DevServiceProcessResolver.HasExplicitProcessName(config));
    }

    [Fact]
    public void ResolveProcessName_FallsBackToStartCommand()
    {
        var config = new DevServiceConfig
        {
            StartCommand = @"bin\postgres -D data",
            ProcessName = string.Empty,
        };

        Assert.Equal("postgres", DevServiceProcessResolver.ResolveProcessName(config));
        Assert.False(DevServiceProcessResolver.HasExplicitProcessName(config));
    }

    [Theory]
    [InlineData(@"D:\App\DB\redis-8.4.0\redis-server.exe", @"D:\App\DB\redis-8.4.0", true)]
    [InlineData(@"D:\App\DB\redis-8.4.0\bin\redis-server.exe", @"D:\App\DB\redis-8.4.0", true)]
    [InlineData(@"D:\App\DB\redis-8.4.0\redis-server.exe", @"D:\App\DB\redis-8.4.0\", true)]
    [InlineData(@"D:\App\DB\redis-8.4.0\redis-server.exe", @"D:/App/DB/redis-8.4.0", true)]
    [InlineData(@"D:\App\DB\redis-8.4.0-beta\redis-server.exe", @"D:\App\DB\redis-8.4.0", false)]
    [InlineData(@"D:\App\DB\other\redis-server.exe", @"D:\App\DB\redis-8.4.0", false)]
    [InlineData(@"D:\App\DB\redis-8.4.0\redis-server.exe", "", true)]
    [InlineData("", @"D:\App\DB\redis-8.4.0", false)]
    public void IsPathInside_RespectsDirectoryBoundary(string filePath, string directory, bool expected)
    {
        Assert.Equal(expected, DevServiceProcessResolver.IsPathInside(filePath, directory));
    }
}
