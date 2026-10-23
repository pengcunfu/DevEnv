namespace DevEnv.Core.Models;

/// <summary>
/// 服务层统一返回值：成功可带提示，失败带错误文案。
/// 替代原先 Minimal API 的 200/400 + <c>{ error }</c> 组合。
/// </summary>
public readonly record struct OperationResult
{
    private OperationResult(bool ok, string? error, string? message)
    {
        Ok = ok;
        Error = error;
        Message = message;
    }

    public bool Ok { get; }
    public string? Error { get; }
    public string? Message { get; }

    public static OperationResult Success(string? message = null) => new(true, null, message);
    public static OperationResult Fail(string error) => new(false, error, null);

    /// <summary>给界面直接显示的一行文案。</summary>
    public string DisplayMessage => Ok ? Message ?? "完成" : Error ?? "失败";
}