namespace DevEnv.Core;

/// <summary>
/// 设置对话框的分区内容提供者。
///
/// 设置对话框是平铺的三页签：常规 / 语音与截屏 / 服务与模型。
/// 宿主负责页签骨架与「常规」页的主程序设置，各模块通过本契约把自己的分区内容
/// 交给宿主注入，不要在 Core 里直接碰 UI，也不要另起模态设置框。
/// </summary>
public interface ISettingsSections
{
    /// <summary>「常规」页签里追加的模块常规选项（与主程序设置同页展示）。</summary>
    object General { get; }

    /// <summary>「语音与截屏」页签的内容。</summary>
    object Voice { get; }

    /// <summary>「服务与模型」页签的内容。</summary>
    object Service { get; }
}
