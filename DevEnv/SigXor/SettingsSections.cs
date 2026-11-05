namespace DevEnv.Host.SigXor;

using DevEnv.Core;

/// <summary>
/// SigXor 模块在设置对话框里的三个分区内容。
///
/// 设置对话框是平铺的三页签，模块只提供内容，不自己弹窗。
/// </summary>
internal sealed class SettingsSections : ISettingsSections
{
    public SettingsSections(object general, object voice, object service)
    {
        General = general;
        Voice = voice;
        Service = service;
    }

    public object General { get; }

    public object Voice { get; }

    public object Service { get; }
}
