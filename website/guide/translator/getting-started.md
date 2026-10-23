# 熔岩翻译 · 快速开始

「熔岩翻译」是 DevEnv 内嵌的网页翻译助手，在 WebView2 中打开有道、搜狗、百度、谷歌、DeepL、必应、腾讯等翻译站点，支持全局快捷键填入剪贴板文本。

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
2. 左侧导航选择 **熔岩翻译**。
3. 默认翻译引擎在 DevEnv 左侧 **设置 → 熔岩翻译** 中选择；关于与快捷键说明同在设置页。

## 快捷键 Alt+Space

- 剪贴板**有文本**：显示主窗口 → 切到「熔岩翻译」→ 自动填入当前站点输入框。
- 剪贴板**无文本**：显示 / 激活 DevEnv 主窗口。

与 DevEnv 设置中的「显示窗口」热键相互独立。

## 配置目录

翻译站点偏好等保存在：

```
%USERPROFILE%\.lava_translator\config.json
```

与 DevEnv 的 `文档\FNSoftware\DevEnv\` 相互独立。

## 说明

- 关于、开机自启与托盘请在 DevEnv「设置」中查看与管理（本 Tab 仅保留网页翻译）。
- 应用更新走 DevEnv 自更新，不再使用独立 Velopack 通道。
