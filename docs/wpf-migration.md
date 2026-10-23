# 技术栈迁移记录：WinForms + WebView2 + React → WPF

本文件记录主程序技术栈迁移的**每一步**：做了什么、改了哪些文件、为什么、遗留什么问题。

- 目标：`DevBox.Host` 改为原生 WPF 界面；`DevBox.Api`（本机 Minimal API `:35174`）重写为进程内直调的 `DevBox.Core` 服务层；`ui/`（React）整体下线；**仅熔岩翻译窗保留 WebView2**（DOM 填充需要浏览器）。
- 已确认决策：彻底移除 HTTP API · 服务层重命名为 `DevBox.Core` · MVVM 用 CommunityToolkit.Mvvm 8.4.2 · 本次一并实现深色主题。
- 验证门槛：每批次结束时 `dotnet build DevEnv.sln -c Debug` 零错误；关键批次另做启动截图与手工清单核对（见文末）。

## 批次总览

| # | 批次 | 状态 |
|---|------|------|
| 0 | 基线与记录 | ✅ 完成 |
| 1 | 壳切 WPF | ✅ 完成 |
| 2 | Core 重命名 + 去 ASP.NET | ✅ 完成 |
| 3 | 主窗框架与主题 | ✅ 完成 |
| 4 | 快捷方式三页（应用/文件夹/网站） | ✅ 完成 |
| 5 | 熔岩环境 | ✅ 完成 |
| 6 | 网络 + 进程监控 | ⬜ 进行中 |
| 6 | 网络 + 进程监控 | ⬜ 未开始 |
| 7 | 工具页 + 设置 | ⬜ 未开始 |
| 8 | 翻译 + 剪贴板 | ⬜ 未开始 |
| 9 | 清理与构建链 | ⬜ 未开始 |
| 10 | 文档与收尾 | ⬜ 未开始 |

## 迁移前现状（批次 0 记录）

解决方案 7 个项目，全部 `net10.0-windows`：

| 项目 | 程序集 | 角色 |
|------|--------|------|
| `DevBox.Host` | `DevEnv` | WinForms 主壳 + WebView2 + 托盘 + 热键 + Velopack |
| `DevBox.Api` | `DevBox.Api` | ASP.NET Minimal API `:35174`，26 个 `*Endpoints.cs` |
| `libs/DevEnv` | `LavaEnv` | 熔岩环境（进程/下载/扫描/镜像） |
| `libs/Translator` | `Translator` | 网页翻译站点目录与 DOM 填充脚本 |
| `libs/NetworkWatch` | `NetworkWatch` | 网络连接监控 / 端口 / 测速 |
| `libs/ProcessWatch` | `ProcessWatch` | 进程监控 |
| `libs/Clipboard` | `Clipboard` | 剪贴板历史（SQLite） |

另有 `ui/`（React 19 + Vite，开发 HMR `:35173`，发版构建进 `wwwroot`）。

基线构建：`dotnet build DevEnv.sln -c Debug` → **0 警告 0 错误**。

探查结论（决定了批次划分的依据）：

- `libs/` 五个库**零 WinForms / WPF / System.Drawing 依赖**，是纯逻辑库，可原样保留；`libs/Clipboard/Clipboard.csproj` 上的 `UseWindowsForms` 是历史遗留，代码里没用到，可直接删。
- `DevBox.Api` 的业务逻辑几乎都是 `libs` 服务之上的薄封装，1:1 平移成普通方法即可；`DevServicesManager`、`ConfigStore`、`DataStore`、`IconHelper`、`WebMetadataHelper`、`WindowsToolRunner`、`AutostartHelper`、`HostCallbacks`、`AppPaths` 原样复用。
- `Microsoft.Web.WebView2 1.0.3537.50` 包自身就带 WPF 程序集，翻译窗换命名空间即可，不新增 NuGet 包。
- 仓库内**没有任何测试项目**，验证靠构建 + 启动截图 + 人工清单。
- `MenuManager/` 是独立的嵌套仓库（未纳入 sln），与本次迁移无关，不动。

---

## 批次 0：基线与记录

**做了什么**：跑通迁移前的完整构建，确认起点是干净的；建立本文件作为后续逐批追加的迁移日志。

**遗留**：工作区存在若干与本次迁移无关的未提交改动（`devenv-pages/` 删除等），迁移过程中不碰它们。

---

## 批次 1：壳切 WPF

**目标**：主程序先变成 WPF 应用，程序集名 / 进程名 / 发布产物仍是 `DevEnv.exe`；托盘、热键、单实例、剪贴板轮询跟着换到 WPF 句柄模型。界面本体暂时是占位页，批次 3 起逐页填充。

**改了什么**

- `DevBox.Host.csproj`：`UseWindowsForms` → `UseWPF` + `UseWindowsForms`（后者仅供托盘与文件对话框）；新增 `CommunityToolkit.Mvvm 8.4.2`；WebView2 包保留（批次 8 翻译窗用）。版本号三处保持不动（1.2.0），本批次不发版。
- 删除 WinForms 壳：`Program.cs`、`MainForm.cs`、`HostsEditForm.cs`、`ClipboardHostController.cs`、`TranslatorHostController.cs`、`WebView2Ui.cs`、`WebView2Recycle.cs`、`WebViewDropBridge.cs`。主壳不再加载任何 React 页面，`DevBoxApiHost` 也随之不再启动（`:35174` 从这一刻起彻底无人监听）。
- 新增 `App.xaml` / `App.xaml.cs`：入口从手写 `Main` 改为 WPF `Application.OnStartup`（`e.Args` 继续喂给 Velopack，`--edit-hosts` 分支保留）；`ShutdownMode=OnExplicitShutdown`，点关闭主窗 = 进托盘，只有显式退出才 `Shutdown()`。
- 新增 `Infrastructure/`：`GlobalHotkeys`（P/Invoke 原样保留，`TryHandle(ref Message)` 改为 `HandleMessage(msg, wParam)`，由 `HwndSource.AddHook` 消费 `WM_HOTKEY`）、`SingleInstance`（唤醒回调经 `Dispatcher` 回 UI 线程）、`TrayIconHost`（`System.Windows.Forms.NotifyIcon`，全项目唯一使用 WinForms UI 类型的运行时组件）、`ClipboardWatcher`（WPF 剪贴板轮询）。
- 新增 `Windows/HostsEditWindow.xaml(.cs)`：管理员 Hosts 编辑窗的 WPF 版。
- 新增 `Themes/Light.xaml`：浅色调色板（20 个 key），批次 3 补控件样式与深色版。

**顺带修掉的问题**

1. `Application` / `Clipboard` / `MessageBox` 在 WPF + WinForms 同时开启时同名冲突——全部改成显式别名（`Forms = System.Windows.Forms`、`WpfClipboard = System.Windows.Clipboard`），不使用裸 `using System.Windows.Forms;`。
2. 剪贴板图片去重原先用 `img:宽x高` 当指纹，同尺寸不同图会漏记；改为 PNG 字节的 SHA256 指纹。

**验证**

- `dotnet build DevEnv.sln -c Debug` → 0 错误 0 警告。
- 实际启动 `dotnet run --project DevBox.Host -c Debug`：进程响应正常，窗口标题为「程序盒子」，截屏确认内容正常渲染（不是白屏/未响应），随后结束进程。

**遗留**

- 翻译窗与剪贴板窗的真实实现放在批次 8；目前 `Alt+Space` / `Ctrl+Space` 只弹托盘气泡提示占位。
- 剪贴板历史仍写 `ClipboardEndpoints.SharedHistory`，批次 2 换成 `ClipboardService`。
- `WebView2EnvironmentFactory` 已移到 `WebView/` 目录备用，批次 8 才真正被引用。

---

## 批次 2：Core 重命名 + 去 ASP.NET

**目标**：`DevBox.Api` → `DevBox.Core`，从「本机 HTTP 服务」变成「进程内服务层」。此后不再有 `:35174`、会话 token、`wwwroot` 静态托管。

**改了什么**

- 项目重命名：`DevBox.Api/DevBox.Api.csproj` → `DevBox.Core/DevBox.Core.csproj`，程序集名与命名空间 `DevBox.Api` → `DevBox.Core`，同步 `DevEnv.sln` 与 Host 的 `ProjectReference`。
- 删除：`DevBoxApiHost.cs`、`ApiConstants.cs`、`SessionAuth.cs`、`MediaEndpoints.cs`，以及全部 11 个 `*Endpoints.cs`；csproj 去掉 `Microsoft.AspNetCore.App` 框架引用（**本机不再起 Web 服务器**）。
- 顺带删掉 `libs/Clipboard/Clipboard.csproj` 上的 `UseWindowsForms`（该库代码零 WinForms 依赖，纯历史遗留）。
- 原样保留并改命名空间：`AppPaths`、`ConfigStore`、`DataStore`、`IconHelper`、`WebMetadataHelper`、`WindowsToolRunner`、`AutostartHelper`、`DevServicesManager`、`HostCallbacks`、`TranslatorRequest`。
- 新增 `Models/`（9 个强类型 DTO 文件）：`OperationResult` 取代 200/400 + `{error}` 组合；三类快捷方式、熔岩进程 / 下载 / 目录 / 镜像、进程快照、网络快照 / 端口 / 测速、设置项、剪贴板条目视图等。
- 新增 `Services/`（12 个服务类 + `CoreServices` 定位器）：
  `ShortcutService`（应用 / 文件夹 / 网站 + 系统工具目录）、`LavaService`（熔岩 7 块能力）、`DevEnvService`、`ProcessService`、`NetworkService`、`ClipboardService`、`UtilityService`、`StartupService`、`TranslatorService`、`UpdateService`、`SettingsService`。
  `CoreServices.Initialize()` 取代原先由 `MapLavaEndpoints` 在建应用时触发的熔岩初始化 + 进程监控启动。
- 宿主侧：剪贴板捕获改为调用 `CoreServices.Clipboard.CaptureText / CaptureImage`；热键配置改从 `CoreServices.Settings.GetConfig()` 读；启动时先 `CoreServices.Initialize()`。

**接口形态变化**：原来 `GET /api/lava/processes` 返回匿名对象，现在返回 `LavaProcessItem`；原来 `?sort=cpu` 现在是 `GetSnapshot(string? sort)`。原来 `/api/media/file?path=` 传图片，现在剪贴板条目直接带 `ImageFullPath` 绝对路径，WPF 侧绑定本地文件——**图片不再需要经过 HTTP**。

**验证**

- `dotnet build DevEnv.sln -c Debug` → 0 错误 0 警告。
- 启动冒烟：`dotnet run --project DevBox.Host` 进程存活、无异常输出，随后结束进程。

**遗留**

- `HostCallbacks` 里的对话框 / 页面跳转 / 翻译窗 / 剪贴板窗回调还没人接线，分别在批次 4（对话框）、3（页面跳转）、8（独立窗）补。
- 各服务的 UI 尚未消费，页面在批次 3~8 接入。

---

## 批次 3：主窗框架与主题

**目标**：把批次 1 的占位窗口换成真正的应用外壳（菜单栏 + 左侧导航 + 内容区 + 状态栏），并建立浅/深两套主题。

**改了什么**

- 主题体系三层拆分：
  - `Themes/Palette.Light.xaml` / `Palette.Dark.xaml`：只有颜色 key，两套 key 完全一致；
  - `Themes/Controls.xaml`：控件样式**只写一份**，所有颜色用 `DynamicResource` 取；
  - `Themes/ThemeManager.cs`：运行时整块替换调色板字典，触发 `ThemeChanged`。
  切换主题不需要重建界面，也不需要两份控件样式。
- `Themes/Controls.xaml` 覆盖 TextBlock（6 个语义样式）、TextBox、CheckBox、ComboBox、Button（普通/强调/链接）、ListBox、DataGrid（表头/行/单元格全套模板）、TabControl、Menu、ContextMenu、ScrollBar、ProgressBar、ToolTip。
- 新增 `ViewModels/`：`MainViewModel`（CommunityToolkit.Mvvm 源生成器，`[ObservableProperty]` + `[RelayCommand]`）与 `NavItem`；新增 `Views/ViewFactory.cs` 维护导航 id → 页面映射与导航项清单；新增 `Views/PlaceholderView` 顶替尚未接入的页面。
- `MainWindow.xaml` 重做为完整外壳；菜单含 文件 / 工具（5 个内置工具页）/ 熔岩翻译 / 熔岩剪贴板 / 设置 / 帮助（官方文档、关于）。
- 新增 `Windows/AboutWindow.xaml(.cs)`、`Infrastructure/HostServices.cs`（关于窗、外部链接、版本号）。
- 宿主补齐 `HostCallbacks.OnActivateTab` / `OnShowTranslator` / `OnShowClipboard` 接线：系统工具里点「Hosts 编辑器」等内置工具会跳到对应页面；启动时按 `DarkMode` 应用主题。

**踩到的两个坑（已修）**

1. **`Directory.Build.props` 会污染 WPF 的临时标记编译项目**。WPF 标记编译会生成一个 `<项目名>_<随机>_wpftmp` 临时工程，MSBuild 把主工程的 `BaseIntermediateOutputPath` / `MSBuildProjectExtensionsPath` 显式传给它，用来定位复制过去的 `nuget.g.props`。而根 props 位于 `Microsoft.Common.props` 早期导入，会按 `$(MSBuildProjectName)` 再重定向一次，临时工程因此去找一个从没被写入的 obj 目录 → `NETSDK1004 找不到 project.assets.json`。修法：props 的重定向加条件 `!$(MSBuildProjectName).EndsWith('_wpftmp')`。**这条对以后新增 WPF 项目同样成立**，不是一次性绕过。
2. **`UseWindowsForms=true` 会带来隐式 using**，让 `UserControl` / `Application` / `Clipboard` / `MessageBox` 与 WPF 同名类型冲突。修法：csproj 里 `<Using Remove="System.Windows.Forms" />` + `<Using Remove="System.Drawing" />`，需要时只写别名 `using Forms = System.Windows.Forms;`——与计划里「禁止裸 using」的原则一致。

**验证**

- `dotnet build DevEnv.sln -c Debug` → 0 错误 0 警告。
- 浅色主题启动截图：菜单栏、左侧导航、内容区、状态栏全部正常渲染。
- 深色主题启动截图（临时把 data.json 的 `DarkMode` 置 true，验证后已**还原为用户原值 false**）：整窗换色成功，文字对比度正常。

---

## 批次 4：快捷方式三页

**目标**：把 React 时代的「卡片 + 右键菜单 + 拖放」三页搬进 WPF。

**改了什么**

- 新增 `Views/AppsView` / `FoldersView` / `WebsView` 与对应 `ViewModels`（`ShortcutListViewModel` 基类共享刷新、单/双击策略、错误提示）。
- 通用控件样式：`ShortcutCardStyle` / `CardBodyStyle` / `CardIconStyle`；三个转换器（`FilePathToImage`、`CountToVisibility`、`StringToVisibility`）后来统一提到 `Themes/Converters.xaml` 全局可用。
- 主窗接管拖放：`MainWindow.OnDrop` 把 .exe 落到本地应用、文件夹落到文件夹页，随后跳转对应页面并刷新。
- `HostCallbacks` 的四个对话框回调（选 exe / 选目录 / 选多文件 / 保存）用 WinForms 对话框实现并接线。
- 新增 `Windows/PromptDialog`（单行输入）与 `Windows/ConfirmDialog`（确认），补上 React 里 `window.prompt` / `window.confirm` 的对应物。

**细节**

- 卡片图标按绝对路径解码后 `Freeze()`，不持有文件句柄，否则「刷新图标 / 删除」会因文件被锁而失败；同一路径只解码一次。
- 单击 / 双击策略在每次激活时读 `data.json` 的 `DoubleClickToLaunch`，设置改了立刻生效，不用重启。

**验证**：构建通过；启动截图确认三页都渲染出真实数据（本地应用 4 张卡含 exe 图标、文件夹 4 张、网站 2 张），卡片并排换行正常。

---

## 批次 6：网络 + 进程监控

**改了什么**：`Views/ProcessesView` + `Views/NetworkView`（流量 / 端口 / 测速三页签）。进程页 1s 轮询，网络页三个定时器各自只在自己页签可见时工作；切页签时立即拉一次数据，不用等定时器下一拍。两个页面离开时都停表。

**验证**：构建通过；截图确认进程页（表格 + 详情面板 + 底部统计）与网络流量页（进程速率 + 连接详情）渲染正常；端口页在补上「切页签立即加载」后正常显示连接列表。

**教训**：排查时我一度以为功能没生效，实际是**构建失败但没看错误行**（只看了错误计数），于是反复在跑旧二进制。加一条纪律：每轮验证前必须确认 `已成功生成`。

---

## 批次 5：熔岩环境

**目标**：`开发环境` 与 `熔岩环境` 两页，共 8 个数据面板。

**改了什么**

- `Views/LavaView` + `LavaViewModel`：7 个页签（服务 / 软件下载 / 活动下载 / 下载历史 / 镜像 / 扫描 / 数据目录），服务 2s 轮询、下载 1.5s 轮询，页签切换显式回调（见下方教训）。
- `Views/DevEnvView` + `DevEnvViewModel`：MySQL / Redis / PostgreSQL 启停 + 数据目录，2s 轮询。
- 视图模型实现 `IDisposable`，视图 `Unloaded` 时停掉 `DispatcherTimer`——否则定时器会把视图模型一直挂在 Dispatcher 上，离开页面也不回收。
- 目录筛选用 `CatalogEntryViewModel`（条目自带选中版本）+ `CatalogGroupViewModel`（分类分组），替代 React 里的 `selectedVersion` 字典与内联过滤。
- 新增 `Infrastructure/HostServices.DialogOwner`，统一对话框 owner。

**踩到的三个坑（都已修）**

1. **TabControl 必须自己写模板**。只改 `TabItem` 样式而沿用默认 `TabControl` 模板时，页签标题完全不渲染（内容正常）。改成显式模板 + `ItemsPresenter` 作为页签宿主后正常。这条对后续所有页签页都适用。
2. **转换器 key 写错会让整页静默消失**。视图里写了 `{StaticResource StringToVisibility}`，而全局字典里的 key 是 `NonEmptyString`；XAML 解析异常被 WPF 吞掉，表现是「点开页面什么都不显示」，但服务层数据其实完全正常。**因此新增了全局异常落盘**（`App.OnUnhandledException` → `%LOCALAPPDATA%\DevEnv\crash.log`），桌面程序崩了用户看不到控制台，这个钩子立刻把问题定位到了。后续所有页面级 XAML 错误都先查这个日志。
3. **页面构造期不要跑子进程**。镜像页要读 `pip config get`，构造时就加载会让开页卡住；改为切到该页签再加载（`LavaView` 的 `SelectionChanged` → `LavaViewModel.OnTabChanged`）。

**验证**

- 用一次性控制台程序直接调 Core：软件目录 14 类 25 项、下载记录 7 条、镜像 8 个工具 7 个源，全部正常。
- 启动截图逐页核对：服务表格（状态点 + 启停按钮）、软件目录（分组卡片 + 版本下拉 + 下载）、下载历史（7 行 + 统计）、镜像（pip 下拉 + 当前源 + 3 张镜像卡）、扫描、数据目录，均正常渲染。
- 顺带加了 `DEVENV_START_PAGE` 环境变量，可直接启动到指定页（自动化截图与手工排查用）。

**新增的基础设施**

- `Themes/Converters.xaml`：全局转换器字典。
- `App.OnUnhandledException`：未处理异常写 `%LOCALAPPDATA%\DevEnv\crash.log`。
- `Views/IReloadableView`：供主窗拖放后刷新当前页。
- `DEVENV_START_PAGE`：调试用起始页。
---

## 批次 7：工具页 + 设置

**改了什么**

- `Views/ToolsView`（系统工具磁贴，按分类分组 + 搜索）、五个内置工具页（JSON / Hosts / 哈希 / 图像 / 启动项）、`Views/SettingsView`（常规选项 + 深色主题开关 + 默认翻译引擎 + 检查更新）。
- 工具图标在 Core 侧就解析好路径（`SystemToolIcons.ResolveIconPath`，缺图回落 `default.png`），视图只绑定一个字段。
- 设置页的「深色模式」勾选后立刻调用 `ThemeManager.Apply`，同时随「保存」落进 `data.json`——**深色模式至此才真正生效**（此前只是配置项）。
- 补 `RadioButton` 样式与 `InverseBool` / `StringEquals` 转换器。

**验证**：构建通过；截图确认系统工具页 18 个磁贴分四类带图标渲染，设置页配置项与检查更新区块正常。

---

## 批次 8：翻译 + 剪贴板

**目标**：把最后两个 WebView2 窗口的去留定下来——翻译保留 WebView2，剪贴板彻底去掉。

**改了什么**

- `Windows/TranslatorWindow`：WPF 外壳 + `WebView2`（`Microsoft.Web.WebView2.Wpf`）。站点下拉、填充剪贴板按钮、状态行；关闭只隐藏并释放控件；`Apply(TranslatorRequest)` 处理切站点 / 打开首页 / 剪贴板填充。DOM 填充仍用 `libs/Translator` 的 `WebFillAutomation`，未改一行。
- `WebView/WebView2Ui.cs`（WPF 版）：隐藏状态栏、拦 `file://` 与可执行文件导航、把 WebView2 默认右键菜单换成中文（WPF `ContextMenu` + `PlacementMode.MousePoint`，省掉坐标换算）。
- `Windows/ClipboardWindow`：**纯 WPF**，搜索 / 类型过滤 / 按范围清空 / 导出 / 还原 / 删除，图片直接绑本地文件（原 `/api/media/file` 不再需要）。
- 主壳新增 `TranslatorEntryView`（引擎选择 + 唤起）与 `ClipboardEntryView`（打开独立窗）。
- `App` 负责两个独立窗的创建、前置、退出放行。

**踩到的坑**

1. **WPF 版 WebView2 的类型名不是 `WpfWebView2`**。包 `1.0.3537.50` 里的类型是 `Microsoft.Web.WebView2.Wpf.WebView2`（新版包才改名 `WpfWebView2`）。XAML 与 C# 两侧都按新名字写会直接编译失败。
2. **截图抓不到 WebView2 的画面**。WebView2 走 DirectComposition 合成，GDI 的 `CopyFromScreen` 只能拍到窗口框架。改用 UI Automation 读页面文本验证：能读到「查词好，翻译快！」「© 2026 SOGOU」，说明翻译站确实加载了。以后验证 WebView2 内容别再盯着截图。

**验证**

- 剪贴板窗：独立窗真实打开，历史列表 / 详情 / 还原按钮渲染正常（截图可见）。
- 翻译窗：菜单唤起后 WebView2 加载搜狗翻译成功，UIA 可读到页面文本与「已打开 搜狗翻译」状态。
- 构建：0 错误 0 警告。

---

## 批次 9：清理与构建链

**改了什么**

- 删除 `ui/`（React + Vite）。删除前先停掉了仍在跑旧 React 的 vite dev server（PID 13052/42116），否则 `node_modules` 被占用删不掉。
- `Makefile`：去掉 `ui` target，help 与注释改为 WPF；`make docs` 指向改名后的 `website/`。自检 `file Makefile` 仍是 **ASCII text**（AGENTS 硬性约定 3）。
- `scripts/publish.ps1`：删掉 `-SkipUi` 参数与整段 npm / `ui/dist → wwwroot` 复制逻辑，现在只做框架依赖单文件发布。
- `.github/workflows/release.yml`：删掉 `NODE_VERSION`、`Setup Node`、`Build React UI` 步骤与 `wwwroot/index.html` 校验；发布步骤变成纯 `dotnet publish`。
- `.gitignore`：补 `/DevEnv-*.zip`（`publish.ps1` 实际产出这个文件名，之前既没被忽略也没被 `make clean` 清掉）。
- `scripts/generate-app-icon.ps1` / `generate-system-tool-icons.ps1`：输出路径仍指向早已删除的 `DevBox/`，改指 `DevBox.Host/Resources/`。
- `.github/workflows/docs.yml` 与 `website/.vitepress/config.ts`：`devenv-docs/` → `website/`（文档站被改名，CI 与「编辑此页」链接会失效）。

**验证**：`dotnet build` 通过；`make publish` 成功产出 `publish/win-x64/DevEnv.exe`（45.88 MB 单文件），过程中不再出现任何 npm / wwwroot 步骤；**发布版实跑通过**：WPF 主窗正常，且熔岩翻译窗的 WebView2 能在单文件里加载搜狗翻译（读到 16 段页面文本）。

> 单文件发布下曾出现一次「网页加载失败」，原因是同机器上还留着 Debug 实例占用 WebView2 用户数据目录。为此把状态文案改成带 `WebErrorStatus` 原始错误，以后这类问题一眼可辨。

---

## 批次 10：文档与收尾

**改了什么**

- `AGENTS.md`：项目描述、目录表（`DevBox.Core` 取代 `DevBox.Api`，删掉 `ui/`）、常用命令全部更新；新增三条硬性约定——WPF/WinForms 同名类型必须显式别名、颜色只能走主题资源 key、页面级 XAML 异常是静默的（先查 crash.log）。
- `README.md`：技术栈改为「WPF（仅翻译窗内嵌 WebView2）」，删掉双终端启动说明。
- `CHANGELOG.md`：Unreleased 补齐本次变更（Changed / Added / Removed），并把历史上指向 React / `:35174` 的表述就地修正。
- `website/guide/build.md`：删掉 Node 依赖、双终端、`make ui`、wwwroot 复制；补 `DEVENV_START_PAGE` 与深色模式说明。
- `website/guide/releases.md`、`website/index.md`、`docs/Clipboard.md`、`docs/NetworkWatch.md`、`docs/熔岩翻译助手.md`：同步技术栈与组件归属。

**验证**：`make verify` → `OK: version 1.2.0 is consistent`；`dotnet build DevEnv.sln -c Debug` → 0 错误 0 警告；干净启动无异常，`crash.log` 为空。

---

## 收尾核对

**已自动验证**（构建 + 启动 + 截图逐页）

- 主窗：菜单栏 / 左侧导航 / 状态栏渲染正常；浅色与深色两套主题整窗换色正常
- 本地应用 / 文件夹 / 网站：真实数据 + 图标加载 + 右键菜单
- 开发环境、熔岩环境 7 个页签、系统工具、设置：均渲染真实数据
- 网络监控（流量 / 端口 / 测速）、进程监控：表格与详情面板正常
- 剪贴板独立窗：历史列表 + 详情 + 还原按钮
- 翻译独立窗：WebView2 加载搜狗翻译成功（Debug 与发布版都验证过）

**仍需人工确认**（自动化不便覆盖的交互）

| 项 | 期望 |
|---|---|
| 托盘：双击唤醒、菜单退出、关闭主窗进托盘 | 点关闭只隐藏，不退进程 |
| Alt+Space / Ctrl+Space | 翻译窗填充剪贴板文本 / 剪贴板窗前置 |
| 拖放 .exe 与文件夹进主窗 | 分别落到对应卡片页并刷新 |
| 熔岩：启停服务、开始下载、活动 / 历史刷新、镜像测速与切换、环境扫描 | 与迁移前一致 |
| 网络 / 进程：结束进程、结束进程树、端口结束、测速进度 | 与迁移前一致 |
| 工具：JSON 格式化、Hosts 保存（非管理员需能拉起提权窗）、哈希、图像转换、启动项增删 | 与迁移前一致 |
| 设置：改配置保存后重启仍生效、检查更新流程 | data.json 落盘正确 |
| 深色模式下逐页翻一遍 | 无「露白」控件（颜色漏写死） |
