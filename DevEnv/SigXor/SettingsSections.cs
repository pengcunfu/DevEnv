namespace DevEnv.Host.SigXor;

using DevEnv.Core;

/// <summary>
/// SigXor 模块在设置对话框里的四个分区内容。
///
/// 设置对话框是平铺的四页签，模块只提供内容，不自己弹窗。
/// </summary>
internal sealed class SettingsSections : ISettingsSections
{
    public SettingsSections(object general, object hotkeys, object voice, object service)
    {
        General = general;
        Hotkeys = hotkeys;
        Voice = voice;
        Service = service;
    }

    public object General { get; }

    public object Hotkeys { get; }

    public object Voice { get; }

    public object Service { get; }
}
