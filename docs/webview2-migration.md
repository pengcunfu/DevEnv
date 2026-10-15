# WinForms + WebView2 + React 迁移说明

`make run` 仍启动 Avalonia 旧壳。新壳：

```bash
# 终端 1
make run-host CONFIG=Debug

# 终端 2
make ui
```

| 端口 | 用途 |
|------|------|
| `35173` | Vite HMR UI（`--strictPort`） |
| `35174` | 本机 API（axios） |

## 已接通（新壳）

- 壳：无边框标题栏（最小化/最大化/关闭）、托盘、单实例、拖放、剪贴板轮询
- 全局热键：显示窗口（可配置）· Alt+Space 翻译（带剪贴板文本）· Ctrl+Space 剪贴板
- 本地应用 / 文件夹 / 网站：列表、图标、启动/打开、添加、重命名、删除、打开目录、favicon 抓取
- 开发环境：MySQL / Redis / PostgreSQL 启停与数据目录
- 设置：Config、开机自启、托盘、Velopack 检查更新、启动项入口
- 系统工具：Windows 管理工具 + JSON/Hosts/哈希/图像/启动项 Web 页
- 进程监控：CPU/内存、结束进程/树、打开目录
- 网络监控：流量快照、端口管理、测速、管理员提权/降权重启
- 熔岩环境：绿色版启停、下载管理、镜像配置、环境扫描结果表
- 熔岩剪贴板：历史、搜索、分段清空、导出、图片预览、还原到系统剪贴板
- 熔岩翻译：宿主 WebView2 + 站点工具栏、WebFillAutomation DOM 填充、Alt+Space（有文本则填充）、设置默认引擎

## 仍待补齐 / 弱对等

- 启动项：服务 / 计划任务完整管理（当前仅 HKCU Run + 启动文件夹）
- 网络端口双击进程详情窗
- 剪贴板热键自定义 UI（当前固定 Ctrl+Space）
- Dark mode 实际套用主题
- 默认入口 cutover（切换 `make run`）并移除 Avalonia
