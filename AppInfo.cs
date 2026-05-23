using System.Reflection;

namespace DevEnv;

/// <summary>应用版本与产品信息，与 DevEnv.csproj 中的 Version 同步。</summary>
public static class AppInfo
{
    /// <summary>产品全称。</summary>
    public const string ProductName = "熔岩环境管理工具";

    /// <summary>界面短名称。</summary>
    public const string ProductShortName = "熔岩";

    /// <summary>产品定位一句话描述。</summary>
    public const string ProductTagline = "开源智能开发环境管理工具，为多语言与全栈开发者提供一键下载、配置与管理";

    /// <summary>GitHub 仓库地址。</summary>
    public const string RepositoryUrl = "https://github.com/pengcunfu/devenv";

    /// <summary>GitHub Pages 帮助文档首页。</summary>
    public const string DocsUrl = "https://pengcunfu.github.io/devenv/";

    /// <summary>发布包文件名前缀（ASCII，避免路径编码问题）。</summary>
    public const string ReleaseArtifactPrefix = "LavaEnv";

    public static string Version
    {
        get
        {
            var informational = typeof(AppInfo).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informational))
                return informational.Split('+')[0];

            return typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }
    }
}
