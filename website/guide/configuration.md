# 配置说明

配置文件位于用户文档目录：**`文档\FNSoftware\DevEnv\`**（`FNSoftware` 为软件提供商，`DevEnv` 为程序目录）。

旧版若仍在 `文档\FNSoftware\DevBox\`，首次启动会把 `data.json` / `icons` 迁入新目录。

## data.json

主程序业务数据与偏好（`Config` 段）都在这里，由程序维护。**不建议手动编辑**，除非备份后用于迁移或恢复。偏好设置示例：

```json
{
  "Config": {
    "TrayIcon": true,
    "SelfStarting": false,
    "BaseEnvPath": "D:\\ProgramBox\\env",
    "Language": "zh-CN",
    "DarkMode": false
  }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `TrayIcon` | bool | 启用托盘；关闭窗口时隐藏到托盘后台运行（无边框窗口右上角 ✕ 与 Alt+F4 均生效） |
| `SelfStarting` | bool | 是否开机自启（写入注册表 Run 项） |
| `BaseEnvPath` | string | 开发环境根目录 |
| `Language` | string | 界面语言，如 `zh-CN` |
| `DarkMode` | bool | 深色模式 |

修改后重启应用生效。`SelfStarting` 开启/关闭会同步系统启动项。

同目录下的 `config.json` 属于熔岩环境模块（下载路径、镜像等），与主程序 `data.json` 分开。

迁移到新机器时：

1. 复制 `文档\FNSoftware\DevEnv\` 下的 `data.json`、`config.json` 以及 `icons`（及熔岩的 `apps` / `cache` 如需保留）
2. 根据新机器路径调整 `BaseEnvPath` 与各条目的可执行文件路径

## 备份建议

定期备份 `文档\FNSoftware\DevEnv\` 下的 `data.json` 与 `config.json`。升级安装只需覆盖程序文件，用户数据不受影响。
