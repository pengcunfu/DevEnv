# Changelog

本文件记录各版本变更。发布新版本时请在此补充条目，并打 Git 标签（如 `v1.0.1`）触发 Release 工作流。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。

## [Unreleased]

### Added

- 集成 MenuManager 功能：新增「右键菜单管理」工具，可切换 Win11 右键菜单形态（一级 / 完整二级），并管理「用 XXX 打开」右键菜单项；与 MenuManager 共用 `文档\FNSoftware\MenuManager\menu_configs.json`，配置互通
- 「工具」菜单下的 JSON 格式化器 / Hosts 编辑器 / 图像转换器 / 哈希计算器 / 启动项管理 / 右键菜单管理改为**独立非模态窗口**，同一工具只保留一个窗口实例
- 深色主题：浅 / 深两套调色板 + 运行时切换，控件样式只写一份（颜色走 `DynamicResource`）
- 全局未处理异常落盘 `%LOCALAPPDATA%\DevEnv\crash.log`（桌面程序崩了用户看不到控制台）
- `DEVENV_START_PAGE` 环境变量：调试时直接启动到指定页
- 「进程监控」页：实时查看本机进程 CPU / 内存占用，支持搜索、按 CPU / 内存 / 名称排序、暂停刷新，以及结束进程、结束进程树、在资源管理器中定位
- 翻译 / 剪贴板改为独立窗口（翻译窗用 WebView2，剪贴板窗为纯 WPF）；系统菜单位于窗口菜单栏；熔岩环境下载分「活动 / 历史」页签

### Changed

- **技术栈切换为原生 WPF**：主界面、设置、系统工具、熔岩、网络 / 进程监控、剪贴板全部改为 WPF 原生界面（CommunityToolkit.Mvvm + 两套主题，浅色 / 深色）
- `DevBox.Api` 重命名为 `DevBox.Core`，从「本机 Minimal API `:35174`」改为**进程内直调的应用服务层**（26 个 `*Endpoints` → 12 个服务类 + 强类型模型）；不再起 HTTP 服务、不再有会话 token 与 `wwwroot`
- **删除 `ui/`（React + Vite）**：`make ui` 目标、`publish.ps1` 的 npm/wwwroot 步骤、CI 的 Node 阶段全部移除；构建与运行不再需要 Node.js
- 熔岩剪贴板独立窗改为纯 WPF（此前是加载 React 页面的 WebView2 窗），Ctrl+Space 唤起不变
- 设置页「深色模式」现在真正生效（此前只是配置项，界面不跟随）
- 菜单栏重做：顶栏项紧凑（原被误加的 `MinWidth=160` 撑开）、子菜单向右级联、带箭头/分隔线/悬停高亮
- 图标路径兼容历史数据：`data.json` 里指向迁移前旧目录的 `IconPath` 自动回退到当前图标目录
- 文档站目录 `devenv-docs/` 更名为 `website/`，`docs.yml` 与「编辑此页」链接同步
- 程序英文名 / 进程名 / 程序集名 / Velopack 包 ID 统一为 `DevEnv`（产出 `DevEnv.exe`、`DevEnv-win-Setup.exe`）；界面显示名仍为「程序盒子」
- 熔岩逻辑库程序集改为 `LavaEnv.dll`，避免与主程序 `DevEnv` 程序集名冲突
- 用户数据目录改为 `文档\FNSoftware\DevEnv\`（与熔岩模块共用根目录）；首次启动会从旧版 `文档\FNSoftware\DevBox\` 迁入 `data.json` 与 `icons`
- **注意**：Velopack 包 ID 从 `DevBox` 改为 `DevEnv` 后，旧版安装器无法走增量自更新，需重新安装 `DevEnv-win-Setup.exe`
- JSON 格式化器、Hosts 文件编辑器、图像格式转换器、文件哈希计算器从「熔岩环境 → 工具」移至「系统工具 → 实用工具」
- 默认入口改为 `make run`（Host）；版本号与发布脚本指向 `DevBox.Host/DevBox.Host.csproj`
- 文档站 CI / `make docs` 改指文档站目录
- 逻辑库统一迁至 `libs/`（`Clipboard` / `DevEnv` / `NetworkWatch` / `ProcessWatch` / `Translator`）

### Removed

- 移除本机 HTTP API：`:35174` 端口、会话 token 鉴权、`wwwroot` 静态资源托管（图片不再经 HTTP，直接绑本地文件）
- 移除主壳 WebView2：界面不再嵌入 Chromium，只有熔岩翻译窗保留 WebView2（DOM 填充需要真实浏览器）
- 移除 Avalonia 主程序 `DevBox/` 与共享控件库 `Lava.UI/`
- 内嵌模块迁至 `libs/`，并删除 Views / UI / `*.axaml` 等停用界面源文件；只保留供服务层 / Host 使用的 Services / Models
- 删除未引用的独立项目 `WinBootSelfStarting/`（启动项已由 `DevBox.Core` 承接）与对应过时文档

## [1.2.0] - 2026-10-15

### Added

- 「网络监控」Tab：按进程查看 TCP / UDP 连接与实时上传、下载速率，支持连接详情、按进程名 / PID / 路径搜索、一键测速（延迟 / 抖动 / 节点），以及端口管理（查看连接、终止占用进程、查看进程详情），可切换浅色 / 深色主题
- 「熔岩剪贴板」Tab：自动记录文本与图片剪贴板历史，支持搜索、分类过滤、导出历史与按时间范围清空，可配置全局唤起快捷键
- 「熔岩翻译」改为纯网页翻译 Tab：内置有道 / 搜狗 / 百度 / 谷歌 / DeepL / 必应 / 腾讯等站点，默认引擎改到「设置 → 熔岩翻译」选择，切换后立即生效；`Alt+Space` 在剪贴板有内容时自动填入当前翻译站
- 「熔岩环境」由独立应用并入为 Tab：绿色版进程管理、软件下载、环境扫描与镜像配置，以及 JSON 格式化 / Hosts 编辑 / 图片转换 / 文件哈希等小工具
- 设置页新增「快捷方式启动方式」，可在单击与双击启动之间切换，对本地应用、文件夹、网站与系统工具卡片一致生效
- 设置页「关于」区补充熔岩翻译助手、熔岩剪贴板两个内嵌模块的版本与功能说明
- 新增开发环境下载站（Vue 3 + Vite，优先国内镜像），与文档站一并部署在 Cloudflare Pages

### Changed

- 界面整体紧凑化：卡片圆角、内边距、字号与导航高度统一收紧；主导航由顶部横排改为左侧竖排 Tab，应用卡片加宽并改为左对齐换行
- 产品显示名统一为「程序盒子」（程序集名与用户数据目录仍为 `DevBox`，升级不受影响）
- 升级到 Avalonia 12，并完成窗口装饰、拖放、剪贴板等 API 迁移
- 图片格式转换由 SixLabors.ImageSharp 换成 Magick.NET（Apache-2.0），规避商业许可与 NU1902 / NU1903 告警
- 编译产物统一到仓库根目录 `bin/<项目名>`、`obj/<项目名>`（新增 `Directory.Build.props`）
- 在线文档迁移至 Cloudflare Pages（https://docs.devenv.cn/），本地预览与部署目录由 `docs/` 改为 `devenv-docs/`

### Removed

- 移除「靠边悬停隐藏」功能（依赖收缩态置顶并改写窗口命中测试，交互与稳定性问题较多）
- 移除各内嵌模块的独立发布外壳（安装脚本、CI 工作流与独立入口），打包与自更新统一由 DevBox 承担

### Fixed

- 修复熔岩翻译视图缺少 `using` 导致的编译失败

## [1.1.0] - 2026-09-03

### Added

- Velopack 安装器与增量自更新（设置页「检查更新」，GitHub Releases 作更新源）
- 「开发环境」Tab：以进程模式启动 / 停止本地数据库与缓存服务（MySQL / Redis / PostgreSQL）
- 应用图标更新为青色工具箱设计

### Changed

- 发布方式由 zip 包改为 `DevBox-win-Setup.exe` 安装器（框架依赖单文件，安装器自动安装缺失的 .NET 10 Desktop Runtime）
- 安装后支持应用内自动更新，不再需要手动解压覆盖

## [1.0.0] - 2026-05-24

### Added

- 首个正式版本：Windows 64 位 zip 发布
- 开发环境管理（多语言运行时与版本切换）
- 系统工具快捷入口
- 本地应用、Web 应用与服务管理
- VitePress 在线文档与 GitHub Pages 部署
- MIT 开源协议

[Unreleased]: https://github.com/pengcunfu/DevBox.Net/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/pengcunfu/DevBox.Net/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/pengcunfu/DevBox.Net/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/pengcunfu/DevBox.Net/releases/tag/v1.0.0
