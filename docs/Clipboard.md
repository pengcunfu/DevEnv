# 熔岩剪贴板

剪贴板历史模块，位于 `DevEnv.Core/Clipboard/`，由 `DevEnv.Host` 的剪贴板窗口承载界面。独立窗口是纯 WPF（不再需要 WebView2）；默认 `Ctrl+Space` 唤起。

## 源码位置

```
DevEnv.Core/Clipboard/
├── Models/
└── Services/          # HistoryStore / HistoryService / ConfigService 等
```

## 数据目录

用户数据在 `文档\FNSoftware\DevEnv\`（与主程序共用产品数据根；模块内路径见 `DevEnv.Core.Clipboard.Services.AppPaths`）。

用户使用说明见在线文档：[熔岩剪贴板](https://docs.devenv.cn/guide/clipboard/getting-started)。
