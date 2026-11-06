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
| `ShowWindowHotkey` | string | 显示主窗口的全局组合键，默认 `Ctrl+Alt+D` |
| `QuickAppHotkey` | string | 全局唤起快捷应用的组合键，默认 `Alt+Space` |
| `WinKeyLauncher` | bool | 单独按 Win 键打开全局启动器 |

修改后重启应用生效。`SelfStarting` 开启/关闭会同步系统启动项。快捷键建议在「设置 → 快捷键」页修改，保存后立即重新注册；手工编辑 `data.json` 的快捷键字段后需重启。

### DevServiceList

「开发环境」页的服务列表，由界面添加 / 编辑 / 移除，也可在 data.json 中手工迁移。示例：

```json
{
  "DevServiceList": [
    {
      "Id": "5f0c1c2a8b3d4e5f",
      "Name": "前端开发服务器",
      "WorkingDirectory": "D:\\work\\web",
      "StartCommand": "npm run dev",
      "StopCommand": "",
      "ProcessName": "node"
    }
  ]
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `Id` | string | 条目唯一标识；留空时会自动生成 |
| `Name` | string | 显示名称 |
| `WorkingDirectory` | string | 工作目录；留空时使用用户数据目录 |
| `StartCommand` | string | 启动命令，通过 `cmd /c` 执行 |
| `StopCommand` | string | 可选，停止命令；留空时直接结束进程树 |
| `ProcessName` | string | 可选，进程名（不含 `.exe`）；用于识别状态，并能在重启程序后停止它 |

服务的标准输出与错误输出会实时写入 `文档\FNSoftware\DevEnv\logs\`，文件名形如 `<服务名>-<Id>.log`，每次启动重新写入；页面上的「日志」按钮是查看同一份内容的实时窗口。

同目录下的 `config.json` 属于熔岩环境模块（下载路径等），与主程序 `data.json` 分开。熔岩的镜像清单另存为同目录的 `mirrors_config.json`，可直接编辑，详见 [下载与环境](./lava/download.md#自定义镜像清单)。

迁移到新机器时：

1. 复制 `文档\FNSoftware\DevEnv\` 下的 `data.json`、`config.json`、`mirrors_config.json` 以及 `icons`（及熔岩的 `apps` / `cache` 如需保留）
2. 根据新机器路径调整 `BaseEnvPath` 与各条目的可执行文件路径

## 备份建议

定期备份 `文档\FNSoftware\DevEnv\` 下的 `data.json` 与 `config.json`。升级安装只需覆盖程序文件，用户数据不受影响。
