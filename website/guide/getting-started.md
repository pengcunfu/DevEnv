# 快速开始

## 下载与安装

1. 打开 [GitHub Releases](https://github.com/pengcunfu/DevEnv/releases/latest)
2. 下载 **DevEnv-win-Setup.exe**（Windows 64 位安装器）
3. 运行安装器，按提示安装（若缺少 .NET 10 Desktop Runtime，安装器会自动下载安装）

::: tip
安装后打开「设置」对话框，在「检查更新」中可检查并自动升级，无需重新下载安装包。
:::

## 运行

安装完成后从开始菜单或桌面快捷方式启动「燧岚开发盒子」（进程名 `DevEnv`）。启动后不弹出任何窗口，只在托盘常驻——单击托盘图标打开快捷应用，右键菜单「显示主窗口」进入主界面、「全局启动器…」打开图标启动台，`Ctrl+Alt+D` 也可以唤回主窗口。首次运行会在用户文档目录生成数据：

`文档\FNSoftware\DevEnv\`

| 文件 | 说明 |
|------|------|
| `data.json` | 主程序业务数据与偏好（托盘、自启等） |
| `config.json` | 熔岩环境模块配置 |
| `icons\` | 应用/网站图标缓存 |
| `logs\` | 开发环境服务的运行日志 |

::: tip
用户数据保存在文档目录，升级 / 重装不会丢失。
:::

## 界面概览

- **全局启动器**：主菜单「全局启动器」或单独按一下 `Win` 键打开铺满屏幕的启动器，「本地应用 / 文件夹 / 网站」是三个横向页签；顶部可搜索，单击图标即启动，`Esc` 或点击空白处关闭；把 `.exe` 或文件夹拖入主窗 / 启动器即可添加
- **快捷应用**：在托盘单击图标或主菜单「工具 → 快捷应用」打开，按名称搜索并启动应用、打开文件或执行命令；全局启动器里的本地应用、文件夹与网站也会一起进入搜索结果
- **开发环境**：自定义后台服务的配置、启停与目录访问
- **熔岩环境**：绿色版进程管理、软件下载与镜像配置（见 [熔岩快速开始](./lava/getting-started.md)）
- **熔岩翻译**：网页翻译站与 Alt+Space（见 [翻译快速开始](./translator/getting-started.md)）
- **网络监控**：按进程查看连接与速率、测速与端口管理（见 [网络监控快速开始](./networkwatch/getting-started.md)）
- **进程监控**：实时 CPU / 内存、搜索排序与结束进程（见 [进程监控快速开始](./processwatch/getting-started.md)）
- **服务监控**：Windows 服务启停、启动类型与依赖查看，替代系统服务面板（见 [服务监控快速开始](./servicewatch/getting-started.md)）
- **熔岩剪贴板**：文本/图片历史与全局热键（见 [剪贴板快速开始](./clipboard/getting-started.md)）
- **系统工具**：Windows 管理程序快捷入口，以及 JSON / Hosts / 图像转换 / 哈希等实用工具
- **设置**：托盘图标、开机自启、环境根目录、语言与深色模式、版本更新，以及文档与关于

## 常见问题

**SmartScreen / 杀毒软件拦截？**  
未签名安装器可能被 Windows SmartScreen 提示，选择「仍要运行」即可；可添加信任或使用 [从源码构建](./build.md) 自行编译。

**需要 32 位版本？**  
当前 Release 仅提供 win-x64；如有需求可在 [Issues](https://github.com/pengcunfu/DevEnv/issues) 中反馈。

## 下一步

- [功能概览](./features.md)
- [配置说明](./configuration.md)
- [版本发布](./releases.md)
- [开源协议](./license.md)
