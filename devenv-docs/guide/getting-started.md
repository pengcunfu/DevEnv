# 快速开始

## 下载与安装

1. 打开 [GitHub Releases](https://github.com/pengcunfu/DevBox.Net/releases/latest)
2. 下载 **DevBox-win-Setup.exe**（Windows 64 位安装器）
3. 运行安装器，按提示安装（若缺少 .NET 10 Desktop Runtime，安装器会自动下载安装）

::: tip
安装后在设置页「更新」中可检查并自动升级，无需重新下载安装包。
:::

## 运行

安装完成后从开始菜单或桌面快捷方式启动 `DevBox`。首次运行会在用户文档目录生成数据：

`文档\FNSoftware\DevBox\`

| 文件 | 说明 |
|------|------|
| `data.json` | 应用、环境、服务等业务数据 |
| `config.json` | 托盘、自启动、主题等偏好设置 |
| `icons\` | 应用/网站图标缓存 |

::: tip
用户数据保存在文档目录，升级 / 重装不会丢失。
:::

## 界面概览

- **本地应用 / 文件夹 / 网站**：快捷方式列表与拖拽添加
- **开发环境**：本机服务简易启停
- **熔岩环境**：绿色版进程管理、软件下载与镜像配置（见 [熔岩快速开始](./lava/getting-started.md)）
- **熔岩翻译**：网页翻译站与 Alt+Space（见 [翻译快速开始](./translator/getting-started.md)）
- **网络监控**：按进程查看连接与速率、测速与端口管理（见 [网络监控快速开始](./networkwatch/getting-started.md)）
- **进程监控**：实时 CPU / 内存、搜索排序与结束进程（见 [进程监控快速开始](./processwatch/getting-started.md)）
- **熔岩剪贴板**：文本/图片历史与全局热键（见 [剪贴板快速开始](./clipboard/getting-started.md)）
- **系统工具**：Windows 管理程序快捷入口，以及 JSON / Hosts / 图像转换 / 哈希等实用工具
- **设置**：托盘图标、开机自启、环境根目录、语言与深色模式、版本更新，以及文档与关于

## 常见问题

**SmartScreen / 杀毒软件拦截？**  
未签名安装器可能被 Windows SmartScreen 提示，选择「仍要运行」即可；可添加信任或使用 [从源码构建](./build.md) 自行编译。

**需要 32 位版本？**  
当前 Release 仅提供 win-x64；如有需求可在 [Issues](https://github.com/pengcunfu/DevBox.Net/issues) 中反馈。

## 下一步

- [功能概览](./features.md)
- [配置说明](./configuration.md)
- [版本发布](./releases.md)
- [开源协议](./license.md)
