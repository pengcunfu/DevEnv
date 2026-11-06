namespace DevEnv.Core;

/// <summary>
/// 设置对话框的页签（平铺一层，不搞二级）。
///
/// 常规 = 主程序设置 + 文件预览的常规选项（字体 / 备份）；
/// 快捷键 = 所有程序绑定的快捷键；
/// 语音与截屏 = 语音输入与区域截屏；
/// 服务与模型 = 语音 / OCR 模型与后台服务。
/// </summary>
public enum SettingsSection
{
    General = 0,
    Hotkeys = 1,
    Voice = 2,
    Service = 3,
}
