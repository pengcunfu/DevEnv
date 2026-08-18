# 快速开始

## 下载

1. 打开 [GitHub Releases](https://github.com/pengcunfu/devbox/releases/latest)
2. 下载 **ProgramBox-&lt;version&gt;-win-x64.zip**（Windows 64 位）
3. 解压到任意目录（建议路径不含中文与空格）

## 运行

双击 `ProgramBox.exe` 即可启动。首次运行会在用户文档目录生成数据：

`文档\FNSoftware\DevBox\`

| 文件 | 说明 |
|------|------|
| `data.json` | 应用、环境、服务等业务数据 |
| `config.json` | 托盘、自启动、主题等偏好设置 |
| `icons\` | 应用/网站图标缓存 |

::: tip
可将程序目录加入 PATH，或在桌面创建快捷方式，便于日常启动。
:::

## 界面概览

- **主界面**：应用列表与快捷操作
- **系统工具**：Tab 页内快速打开 Windows 管理程序
- **设置**：托盘图标、开机自启、环境根目录、语言与深色模式，以及文档与关于

## 常见问题

**杀毒软件拦截？**  
自包含单文件发布可能被误报，可添加信任或使用 [从源码构建](./build.md) 自行编译。

**需要 32 位版本？**  
当前 Release 仅提供 win-x64；如有需求可在 [Issues](https://github.com/pengcunfu/devbox/issues) 中反馈。

## 下一步

- [功能概览](./features.md)
- [配置说明](./configuration.md)
- [开源协议](./license.md)
