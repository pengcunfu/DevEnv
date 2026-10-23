using DevEnv.Core.Models;

namespace DevEnv.Tests;

public sealed class OperationResultTests
{
    [Fact]
    public void Success_DefaultsToDone()
    {
        var result = OperationResult.Success();

        Assert.True(result.Ok);
        Assert.Null(result.Error);
        Assert.Equal("完成", result.DisplayMessage);
    }

    [Fact]
    public void Success_KeepsMessage()
    {
        var result = OperationResult.Success("已写入注册表");

        Assert.True(result.Ok);
        Assert.Equal("已写入注册表", result.DisplayMessage);
    }

    [Fact]
    public void Fail_ShowsError()
    {
        var result = OperationResult.Fail("文件不存在：C:\\missing.exe");

        Assert.False(result.Ok);
        Assert.Equal("文件不存在：C:\\missing.exe", result.DisplayMessage);
        Assert.Null(result.Message);
    }

    [Fact]
    public void Default_IsFailed()
    {
        // readonly record struct 的默认值：没赋值就是失败，界面不能把它当成成功
        var result = default(OperationResult);

        Assert.False(result.Ok);
        Assert.Equal("失败", result.DisplayMessage);
    }
}