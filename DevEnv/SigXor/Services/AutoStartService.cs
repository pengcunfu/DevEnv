using System.IO;
using DevEnv.Core;
using DevEnv.Core.Services;

namespace DevEnv.Host.SigXor.Services;

/// <summary>
/// 随 Windows 启动。
///
/// 原实现用启动文件夹快捷方式（.lnk），并入 DevEnv 后统一走宿主的注册表 Run 项：
/// DevEnv 主程序设置里的「开机自启动」（data.json 的 <c>SelfStarting</c>）是唯一事实来源，
/// 落盘由 <c>DevEnv.Core.AutostartHelper</c> 负责（见 <c>DevEnv.Core.Services.SettingsService.Save</c>）。
///
/// 因此本类不再自己写注册表，也不再创建 .lnk：
/// - <see cref="IsEnabled"/> 读注册表的真实状态（能反映用户手动删项）；
/// - <see cref="Apply"/> 写宿主的 <c>SelfStarting</c>，由宿主保存时派发副作用。
///
/// 另需清理旧版遗留的启动文件夹快捷方式，否则会与注册表项重复启动两次。
/// </summary>
public static class AutoStartService
{
    /// <summary>旧版遗留的启动文件夹快捷方式（并入 DevEnv 后不再使用）。</summary>
    private static string LegacyStartupLinkPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), "SigXor.lnk");

    /// <summary>当前是否处于自启状态。以注册表实际状态为准，能反映用户手动删项。</summary>
    public static bool IsEnabled() => AutostartHelper.IsEnabled();

    /// <summary>
    /// 应用自启设置：写入宿主的 <c>SelfStarting</c>，由宿主保存时派发注册表副作用。
    /// </summary>
    /// <param name="enabled">是否开机自启。</param>
    /// <param name="silent">静默启动（开机时不弹任何窗口）。</param>
    public static void Apply(bool enabled, bool silent = true)
    {
        // 清理旧版启动文件夹快捷方式，避免与注册表项重复启动两次
        try
        {
            string legacy = LegacyStartupLinkPath;
            if (File.Exists(legacy))
                File.Delete(legacy);
        }
        catch
        {
            // 清理失败不阻塞应用
        }

        var services = CoreServices.Settings;
        var config = services.GetConfig();
        if (config.SelfStarting == enabled)
            return;

        config.SelfStarting = enabled;
        services.Save(config);
    }
}
