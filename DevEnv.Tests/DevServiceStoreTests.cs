using DevEnv.Core.Models;
using DevEnv.Core.Services;
using Newtonsoft.Json.Linq;

namespace DevEnv.Tests;

public sealed class DevServiceStoreTests
{
    [Fact]
    public void ToJson_RoundTripsAllFields()
    {
        var config = new DevServiceConfig
        {
            Id = "abc",
            Name = "前端开发服务器",
            WorkingDirectory = @"D:\work\web",
            StartCommand = "npm run dev",
            StopCommand = "taskkill /im node.exe /f",
            ProcessName = "node",
        };

        var parsed = DevServiceStore.Parse(DevServiceStore.ToJson([config]));

        var item = Assert.Single(parsed);
        Assert.Equal(config.Id, item.Id);
        Assert.Equal(config.Name, item.Name);
        Assert.Equal(config.WorkingDirectory, item.WorkingDirectory);
        Assert.Equal(config.StartCommand, item.StartCommand);
        Assert.Equal(config.StopCommand, item.StopCommand);
        Assert.Equal(config.ProcessName, item.ProcessName);
    }

    [Fact]
    public void Parse_MissingId_FallsBackToStableIndex()
    {
        var array = JArray.Parse("""[{"Name":"Redis","StartCommand":"redis-server.exe redis.conf"}]""");

        var first = DevServiceStore.Parse(array);
        var second = DevServiceStore.Parse(array);

        Assert.Equal("item-0", first[0].Id);
        Assert.Equal(first[0].Id, second[0].Id);
    }

    [Fact]
    public void Parse_SkipsNonObjectEntries_AndToleratesNullFields()
    {
        var array = JArray.Parse("""[null, 42, {"Name": " A ", "StartCommand": null}]""");

        var parsed = DevServiceStore.Parse(array);

        var item = Assert.Single(parsed);
        Assert.Equal("A", item.Name);
        Assert.Equal(string.Empty, item.StartCommand);
        Assert.Equal("item-2", item.Id);
    }
}
