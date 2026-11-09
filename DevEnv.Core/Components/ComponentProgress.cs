namespace DevEnv.Core.Components;

/// <summary>组件下载/安装进度。<c>Percent</c> 为 -1 表示不确定进度。</summary>
public sealed record ComponentProgress(int Percent, string Message)
{
    public static ComponentProgress Indeterminate(string message) => new(-1, message);
}
