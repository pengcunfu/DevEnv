# 熔岩翻译 · 快速开始

「熔岩翻译」是 DevBox 内嵌的网页翻译助手，在 WebView2 中打开有道、搜狗、百度、谷歌、DeepL、必应、腾讯等翻译站点，支持全局快捷键填入剪贴板文本。

::: tip
无需再单独安装 Translator / 熔岩翻译助手 exe。打开 DevBox，切换到左侧 **熔岩翻译** Tab 即可。
:::

## 系统要求

| 项目 | 要求 |
|------|------|
| 操作系统 | Windows 10 / 11（与 DevBox 相同） |
| 运行时 | 随 DevBox 安装（.NET 10 Desktop Runtime） |
| WebView | [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) |

## 打开方式

1. 启动 DevBox。
2. 左侧导航选择 **熔岩翻译**。
3. 右上角下拉框切换翻译站点；顶栏可进入「设置 / 关于」。

## 快捷键 Alt+Space

- 剪贴板**有文本**：显示主窗口 → 切到「熔岩翻译」→ 自动填入当前站点输入框。
- 剪贴板**无文本**：显示 / 激活 DevBox 主窗口。

与 DevBox 设置中的「显示窗口」热键相互独立。

## 配置目录

翻译站点偏好等保存在：

```
%USERPROFILE%\.lava_translator\config.json
```

与 DevBox 的 `文档\FNSoftware\DevBox\` 相互独立。

## 说明

- 开机自启与托盘请在 DevBox「设置」中管理（本 Tab 不再单独提供）。
- 应用更新走 DevBox 自更新，不再使用独立 Velopack 通道。
