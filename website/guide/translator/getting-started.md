# 熔岩翻译 · 快速开始

「熔岩翻译」是 DevEnv 内嵌的网页翻译助手，在 WebView2 中打开有道、搜狗、百度、谷歌、DeepL、必应、腾讯等翻译站点，支持通过 `Alt+Space` 全局热键打开并填入剪贴板文本。

::: tip
无需再单独安装 Translator / 熔岩翻译助手 exe。打开 DevEnv，切换到左侧 **熔岩翻译** Tab 即可。
:::

## 系统要求

| 项目 | 要求 |
|------|------|
| 操作系统 | Windows 10 / 11（与 DevEnv 相同） |
| 运行时 | 随 DevEnv 安装（.NET 10 Desktop Runtime） |
| WebView | [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) |

## 打开方式

1. 启动 DevEnv。
2. 按 `Alt+Space` 打开翻译窗口（可在「设置 → 快捷键」修改）；也可以从托盘右键菜单「熔岩翻译」打开，或从全局启动器「内置工具」文件夹进入。
3. 默认翻译引擎在托盘右键菜单「设置…」打开的 **「设置」** 对话框中选择。

## 剪贴板填充

- 通过 `Alt+Space` 热键或托盘菜单打开时，剪贴板**有文本**会自动填入当前站点输入框。
- 剪贴板**无文本**时只打开翻译窗口。
- 翻译与剪贴板各占一个全局热键：`Alt+Space` 打开翻译、`Ctrl+Space` 打开剪贴板，均可在「设置 → 快捷键」修改；若组合键被其它程序占用，启动时会用托盘气泡提示注册失败。

## 配置目录

翻译站点偏好等保存在 DevEnv 的用户数据目录：

```
文档\FNSoftware\DevEnv\Translator\config.json
```

旧版位于 `%USERPROFILE%\.lava_translator\` 的配置会在首次启动时自动迁入，迁移成功后旧目录会被清理。

## 说明

- 关于、开机自启与托盘请在 DevEnv「设置」对话框中查看与管理（本 Tab 仅保留网页翻译）。
- 应用更新走 DevEnv 宿主自更新，不再维护独立更新通道。
