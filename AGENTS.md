# AGENTS.md

给在本仓库工作的 AI 编码助手（Claude Code / Cursor / Codex 等）的说明，人类贡献者同样适用。
写代码前先读这一页；与 `Makefile`、`scripts/verify-version.ps1` 冲突时，以脚本的实际校验逻辑为准。

## 项目是什么

**DevEnv**（产品显示名「燧岚开发盒子」，品牌 FireNeb）是 Windows 桌面工具：.NET 10 + 原生 WPF 界面，把开发环境管理和几个自研小工具收进同一个壳。只有熔岩翻译窗内嵌 WebView2（DOM 填充需要真实浏览器）。解决方案文件是 `DevEnv.sln`。

| 路径 | 角色 |
|------|------|
| `DevEnv/` | 主程序（`WinExe`，WPF）：全部界面、托盘、全局热键、剪贴板轮询、可选组件入口与自研自更新；**程序集名 / 进程名为 `DevEnv`**（产出 `DevEnv.exe`） |
| `DevEnv.Core/` | 应用服务层（普通类库）：数据持久化，以及熔岩 / 网络 / 进程 / 剪贴板 / 翻译 / 右键菜单 / 更新等业务编排，按功能分 `LavaEnv/`、`NetworkWatch/`、`ProcessWatch/`、`Clipboard/`、`Translator/` 子目录；界面**进程内直调**，没有 HTTP |
| `DevEnv.Tests/` | 单元测试工程（xunit 2.9 + Microsoft.NET.Test.Sdk）：只引用 `DevEnv.Core`，覆盖纯逻辑、序列化与文件/路径处理；`make test` 执行，CI 发版前也会跑一遍 |
| `website/` | Vue 3 + Vite 官网与文档站（首页 + `guide/**/*.md` 文档 + `/downloads` 软件下载站，正文由 markdown-it 渲染；下载站数据在 `src/data/software.json`）→ Cloudflare Pages，项目名 `devbox-docs`，站点 https://devenv.cn/ |
| `docs/` | 子项目说明文档（纯 Markdown，**不是**文档站，别往里放构建配置） |
| `scripts/` | 发布与版本校验的 PowerShell 脚本 |

**功能模块是 `DevEnv.Core` / `DevEnv` 工程内的子目录，不再有独立工程**。新功能按 `DevEnv.Core/<模块>/{Models,Services}` + `DevEnv/{Views,ViewModels,Windows}` 放置，命名空间与目录一致（`DevEnv.Core.<模块>.*` / `DevEnv.Host.*`）。不要为功能模块恢复独立 `Program.cs`、独立 `sln` 或独立入口；宿主入口统一是 `DevEnv/Program.cs`（只负责分发 `--updater` 助手模式后启动 WPF `App`）。

### SigXor 模块（文件快速预览 / 语音输入 / 截图 OCR 取色）

原 SigXor 独立应用已并入本仓库，代码保留原结构但**命名空间与目录已改写**：

| 位置 | 内容 |
|------|------|
| `DevEnv.Core/SigXor/` | 纯逻辑：HexDocument、文件类型识别、编码检测、Office / RTF / ODF 解析、CSV 解析、Markdown 渲染、快捷键与屏幕几何。命名空间 `DevEnv.Core.SigXor.*` |
| `DevEnv/SigXor/` | WPF 界面与系统能力：预览浮窗、语音输入、截图 / OCR / 取色、文件浏览器。命名空间 `DevEnv.Host.SigXor.*` |
| `DevEnv/SigXor/SigXorHost.cs` | **模块装配入口**。原 SigXor 的 `App` 已改造为此类：不再继承 `Application`、不再自带托盘 / 单实例 / 独立入口（这些统一由 `DevEnv.Host.App` 提供），只保留模块自己的接线与生命周期 |
| `DevEnv/SigXor/SigXorStyles.xaml` | 原 `App.xaml` 里的 `EngineToggle` 样式，已改为 `ResourceDictionary` 并入主 `App.xaml` |
| `DevEnv.Tests/` 根目录 | SigXor 的 12 个测试文件（`namespace DevEnv.Tests.SigXor`），与 DevEnv 原有测试同工程 |

约定：

- **不要再引入第二套基础设施**。托盘、单实例、自启动、更新、异常日志一律用 DevEnv 现有的（`TrayIconHost`、`SingleInstance`、`AutostartHelper`、`CoreServices.Update` + `DevEnv.Core/Updates`）。SigXor 自带的 `TrayIconService` / `SingleInstanceService` / `Updater` / `App` / `Program` 是未接线的遗留代码，**已删除**，不要接回去；模块的更新窗口只能包装 `CoreServices.Update`。
- 模块要暴露能力给宿主，走 `DevEnv.Core.HostCallbacks`（`OnOpenFilePreview` / `OnOpenScreenshot` / `OnOpenSigXorSettings` / `OnOpenSigXorBrowser` / `OnOpenSigXorUpdate`），不要在 Core 里直接碰 UI。
- SigXor 的数据目录仍是 `文档\FNSoftware\SigXor`（与 DevEnv 的 `文档\FNSoftware\DevEnv` 并存），旧版 SigXor 配置与模型在首次启动时自动迁移。
- 全局键盘钩子：SigXor 用自己的 `GlobalHotkeyService`（`WH_KEYBOARD_LL`，处理预览键 / 关闭键 / 语音键 / 反引号），与 DevEnv 的 `RegisterHotKey` 体系**并存但不冲突**（前者管预览与语音，后者管显示窗口、熔岩剪贴板与熔岩翻译）。多个 SigXor 功能共用一个钩子时靠 `GlobalHotkeyRouter` 的**优先级**排序：截图 300 > 语音 200 > 预览 100。快捷键录制期间 `HotkeyCaptureState.IsCapturing` 为 true，路由直接放行按键，避免录制时触发截图 / 语音 / 预览。
- **开机自启只有一个开关**：DevEnv「设置 → 常规」里的 `SelfStarting`（data.json）是唯一事实来源，落盘到注册表 `HKCU\...\Run`。SigXor 的设置页里**不再有**同名勾选框（已并入宿主设置对话框），需要改自启就去宿主的「常规」页签。
- **设置统一在宿主设置对话框里**：SigXor 原独立设置窗口（`DevEnv/SigXor/SettingsWindow.*`）**已删除**，内容拆成四个视图（`DevEnv/SigXor/Views/SigXorGeneralView.xaml` / `SigXorVoiceView.xaml` / `SigXorServiceView.xaml` 与宿主 `DevEnv/Views/HotkeysSettingsView.xaml`），并入燧岚开发盒子设置对话框的**平铺五页签**（**不要二级页签**）：「常规」= 主程序设置 + 文件预览的常规选项，「快捷键」= 所有程序绑定的快捷键，「语音与截屏」，「服务与模型」，「关于与帮助」= 产品信息 + 检查更新 + 帮助文档。中间三页签的内容由 `SettingsDialogHost.Sections`（`ISettingsSections`）在运行时注入（`SigXorHost.CreateSettingsSections`），「关于与帮助」由宿主 `DevEnv/Views/AboutHelpView.xaml` 提供；不要再造第二个模态设置框。主窗口顶部**不再有任何菜单**：设置从托盘右键菜单「设置…」（`TrayIconHost.SettingsRequested` → `SettingsDialogHost.Open()`）进入，内置工具从全局启动器搜索框右侧的「内置工具」文件夹进入。
- **快捷键统一在「设置 → 快捷键」**：显示主窗口 / 熔岩剪贴板 / 熔岩翻译由 `GlobalHotkeys` 通过 `RegisterHotKey` 注册（`data.json` 的 `Config.ShowWindowHotkey` / `Config.ClipboardHotkey` / `Config.TranslatorHotkey`，默认 `Ctrl+Alt+D` / `Ctrl+Space` / `Alt+Space`）；左 Alt 启动器、语音键、截屏组合、文件预览打开 / 关闭键也都在该页修改。全局组合键用 `HotkeyRecorderBox` 录制，录制期间 `HotkeyCaptureState` 会挂起 `GlobalHotkeys` 与 `AltKeyLauncherHook`；常规页保存走 `SettingsService.SaveGeneral`，快捷键页走 `SaveHotkeys`，两者不得互相覆盖。
- **自检探针**：SigXor 有 34 个 XAML（11 个窗口 + 15 个预览视图 + 7 个 WebView2 宿主），而 WPF 的 XAML 异常是静默的（AGENTS.md 第 12 条），靠肉眼点菜单不可靠。改动模块后跑：

 ```bash
 DevEnv\bin\Release\net10.0-windows\DevEnv.exe --sigxor-check %TEMP%\sigxor.txt
 ```

 三组探针：窗口 / 视图能否加载、预览内容能否渲染、深浅主题切换是否生效。单个探针也有独立开关：`--sigxor-smoke` / `--sigxor-preview-smoke` / `--sigxor-theme-smoke`。合并设置对话框后另有 `--settings-check`：验证设置窗口平铺的「常规」「快捷键」「语音与截屏」「服务与模型」「关于与帮助」五个页签都能加载并切换。
- **预览内容类型共 13 种**（`ContentKind`）：Text / Code / Image / Svg / Archive / Csv / Font / Document / Markdown / WebPage / Audio / Video / Pdf / Binary。新增类型要同时改 `DevEnv.Core/SigXor/FileTypeDetector.cs` 与 `DevEnv/SigXor/ViewModels/PreviewViewModel.cs` 的分发，并补一条预览探针用例。
- **空格预览带浏览历史**：`DevEnv.Core/SigXor/PreviewHistory.cs` 在窗口打开期间去重保留预览过的文件，`ShellPreviewViewModel` 用它的 `MovePrevious` / `MoveNext` 提供 `Previous` / `Next`，标题栏左右箭头、`←` / `→` 与原来的 `↑` / `↓` 共用；标题栏「小窗」按钮会保存当前几何后置顶并缩到屏幕右上角，点「还原」恢复大小与位置，小窗期间收起键盘提示与置顶按钮；窗口已打开时在资源管理器或桌面换选文件再按空格会追加预览并保留原队列，选中项没变才按「再按一次空格关闭」处理（`ShellPreviewService._lastSelectionKey`）。预览 / 关闭快捷键都是单键，`OnKeyPressed` 在 `Alt` / `Ctrl` / `Win` 按下时必须直接放行，否则低级键盘钩子会吞掉 `RegisterHotKey` 注册的 `Alt` + 空格（熔岩翻译）等组合键。资源管理器选中项走 `ExplorerSelectionService`（Shell.Application），桌面选中项走 `DesktopSelectionService`（`SHELLDLL_DefView` 下的 `SysListView32`，跨进程读写 `LVITEM`，再按桌面目录回查省略的扩展名）；`InputFocusClassifier.IsDesktopClass` 判断 `Progman` / `WorkerW` / `SHELLDLL_DefView`。
- **webassets 是嵌入 exe 的资源**（`EmbeddedResource` + `LogicalName="webassets/..."`），运行时由 `DevEnv/SigXor/WebAssets.cs` 按内容指纹解压到 `%LOCALAPPDATA%\SigXor\WebAssets\{指纹}`。**不要**改成复制到输出目录——WebView2 的虚拟主机映射只接受真实目录。
- **语法高亮配色跟随主题**（`SyntaxHighlightingService`）：深色主题用 VS Code Dark，浅色主题用 VS Code Light，`ThemeManager.ThemeChanged` 触发后重新注册定义，已打开的 `TextView` 重新取一次定义并刷新。`TextView` 的编辑器底色 / 前景 / 行号颜色必须走 `DynamicResource`（`SurfaceBrush` / `TextPrimaryBrush` / `TextMutedBrush`），**不要写死深色**，否则浅色主题下会是一块黑底。文本预览支持 `Ctrl` + 鼠标滚轮缩放字号（`PreviewTextZoom`，8 到 72），缩放值在会话内沿用，新建文本 / Markdown / 网页源码视图时用 `PreviewTextZoom.Resolve(设置字号)` 取实际字号；在设置里保存新的默认字号后调用 `PreviewTextZoom.Reset()`。其余界面颜色同样一律走 `DynamicResource`，截图 / 取色 / 选区 / 语音浮窗的功能性黑白配色除外（见上一条）。
- **截图 / 取色 / 选区 / 语音浮窗里的黑白配色是功能性颜色**，必须保持不透明与高对比，不随主题变化（`scripts/theme-sigxor-xaml.ps1` 已把它们列为例外）。
- **合并脚本**：`scripts/merge-sigxor.ps1`（搬运）+ `scripts/rewrite-sigxor-namespace.ps1`（改写命名空间）+ `scripts/verify-sigxor-merge.ps1`（逐字校验）。合并 SigXor 新版本后跑一次 verify，确认「除预期的宿主适配外与源仓库逐字一致」。

### 可选组件与自更新

- **基础包只带 Magick.NET 等核心依赖**。以下三类重依赖不在 `DevEnv.exe` 里，改为从 GitHub Release 的组件 zip 按需安装：
  - `office`：`DocumentFormat.OpenXml`（Office 结构化预览，下载约 2 MB，安装后约 8.5 MB）
  - `ocr`：`RapidOcrNet` + `SkiaSharp` + `Clipper2` + `System.Numerics.Tensors` + `onnxruntime`（下载约 12 MB，安装后约 30 MB）
  - `voice`：`org.k2fsa.sherpa.onnx` + `onnxruntime`（下载约 8 MB，安装后约 21 MB）
- 源码仍保留这些 `PackageReference`（直接引用类型，去掉会破坏编译）；`DevEnv/DevEnv.csproj` 的 `RemoveOptionalComponentsFromPublish` target 在生成 deps.json 与单文件 bundle 之前把运行时文件剔除。组件打包用 `-p:DevEnvIncludeOptionalComponents=true` 的完整发布；不要用 `ExcludeAssets` 改编译引用。
- 组件清单 `components.json` 与组件 zip 放在 GitHub Release；App 通过 `https://github.com/pengcunfu/DevEnv/releases/latest/download/components.json` 读取，支持 `urls` 数组回退镜像，`DEVENV_COMPONENTS_MANIFEST` 可覆盖地址。安装目录是 `%LOCALAPPDATA%\DevEnv\Components\<id>\<version>`，状态在 `state.json`；下载校验 SHA256，解压做 zip-slip 校验。
- 运行时由 `DevEnv.Core/Components/ComponentLoader` 的默认 `AssemblyLoadContext.Resolving` 从组件目录加载托管 DLL；**不要**调用 `SetDefaultDllDirectories` 改进程级 DLL 搜索路径。OCR / 语音各自目录内带完整 `onnxruntime` 闭包（同一版本），原生 DLL 与托管 DLL 放在同一个组件目录。
- 缺组件时：Office 结构化预览自动降级到 WebView2 网页引擎并在提示面板给安装入口；截图 OCR / 语音输入给出“请到 设置 → 服务与模型 下载”的提示，不构造引擎；语音自动启动在组件缺失时直接跳过。
- **不再使用 Velopack**。发布物是便携 `DevEnv-<ver>-win-x64.zip`（exe + `Resources/` + 许可证），解压即用；自研更新器在 `DevEnv.Core/Updates/`，流程是 `latest.json` → 下载 zip + SHA256 → 解压暂存 → 启动 `DevEnv.exe --updater apply --payload <staging> --pid <pid> --to <ver>` → 旧进程退出后备份 `DevEnv.exe` 与 `Resources/`、覆盖载荷、启动新版；新版启动成功写 `.update_ok`，30~45 秒看门狗未见标记或进程提前退出则恢复 `.bak` 并重启旧版。日志在 `%LOCALAPPDATA%\DevEnv\updater.log`。
- `UpdateService.CanUpdate` 只在便携目录可写、不是 `DevEnv.dll` 开发构建、也不是旧 Velopack `current\` 布局时为 true；否则关于页提示手动下载。旧 Velopack 用户需要一次性下载便携 zip 迁移。

### 全局启动器（Launchpad）

- 入口：单独按左 Alt 键或托盘右键菜单「全局启动器…」 → `ShortcutWindowHost.Open()` → `DevEnv/Windows/ShortcutsWindow.xaml(.cs)`，使用 Windows 默认窗口边框与标题栏的启动器（`DevEnv.Core/Services/LauncherGeometry.cs`：固定 1200 × 800 DIP，在工作区内水平、垂直居中；内容配色与 Windows 11 开始菜单一致，系统「应用模式」为浅色时用 `Themes/Launcher.Light.xaml`，深色时用 `Themes/Launcher.Dark.xaml`），顶部同一行从左到右是本地应用 / 文件夹 / 网站 / 开始菜单四个**横向页签**、宽 250 的搜索框，以及「系统工具」「内置工具」两个只读文件夹按钮（`ShortcutLauncherViewModel`；页签在搜索态与文件夹浮层打开时用 `Hidden` 隐藏但仍占位，后面的搜索框不会向左跳）。系统工具与内置工具**不是页签**：点击后复用启动器文件夹浮层（`ShortcutLauncherViewModel.OpenSystemToolsShelf / OpenBuiltinToolsShelf`，条目带 `IsReadOnlyGroup`），在居中的文件夹卡片里展开工具图标。系统工具文件夹复用 `ShortcutService.GetSystemTools / RunSystemTool`；内置工具文件夹是宿主原有的 JSON / Hosts / 图像 / 哈希 / 文件解锁 / 启动项 / 右键菜单工具，外加五个窗口入口：「燧岚开发盒子」（开发环境 / 熔岩环境，key 为 `dev-window`，走 `HostCallbacks.OnShowDevelopmentWindow`）、「燧岚监控中心」（网络 / 进程 / 服务监控，key 为 `monitor-window`，走 `HostCallbacks.OnShowMonitorWindow`）、「熔岩翻译」（key 为 `translator`，走 `HostCallbacks.OnShowTranslator` 并填充剪贴板）、「熔岩剪贴板」（key 为 `clipboard`，走 `HostCallbacks.OnShowClipboard`）与「文件浏览器」（key 为 `browser`，走 `HostCallbacks.OnOpenSigXorBrowser`），其余工具直接调用 `ToolWindowHost.Open`，**主窗口顶部不再有「工具」菜单**。代码里的 `ShortcutsWindow` / `ShortcutWindowHost` 是历史类名，界面与文档一律称「全局启动器」。
- **使用 Windows 默认窗口，不再自绘透明外壳**：窗口保持 `WindowStyle="SingleBorderWindow"`（不写 `WindowStyle` / `AllowsTransparency`）与系统标题栏、边框，背景由 `Window.Background` 绑定到 `LauncherBackgroundBrush` 提供，不再抓取桌面做磨砂、不做圆角裁剪与玻璃高光。窗口不播放整窗入场 / 退场动画：打开直接 `Show()`，收起直接 `Hide()`；换页滑入、卡片错峰与文件夹浮层动画保留。配色跟随 **Windows 的系统应用模式**（`HKCU\...\Themes\Personalize\AppsUseLightTheme`），不是 DevEnv 自己的 `DarkMode`；两套调色板 `Themes/Launcher.Light.xaml` / `Launcher.Dark.xaml` 的 key 完全一致，由 `ShortcutsWindow.ApplySystemColorScheme` 在打开前整块替换，新增颜色必须同时加进两套文件并用 `DynamicResource` 引用，不要写死色值。
- 交互：单击图标启动、`Esc` 关闭、`←` / `→` 或 `Ctrl` + 鼠标滚轮切页、`F5` 刷新、拖入 `.exe` / 文件夹添加；右键图标可运行、定位、改名、刷新图标、移除。**点击空白处不再关闭窗口**，而是开始框选；拖动空白绘制选框多选（`Ctrl` + 单击切换选中），拖动卡片调整顺序，把卡片拖到另一个卡片的图标中心合并成启动器文件夹；点击文件夹磁贴会在居中的浮层卡片里展开组内条目（不占满启动器，底层页签仍然可见），浮层没有返回按钮，点击卡片外空白或按 `Esc` 收起。滚轮监听在根 Grid 的 `PreviewMouseWheel`（滚动内容会被 `ScrollViewer` 标记已处理，冒泡阶段收不到），搜索期间与左右键一样不切页；换页时 `OnPageSwitched` 先把网格滚回顶部，再按切换方向播放 24 DIP 滑入 + 淡入（190ms / 150ms，EaseOut）；新生成的卡片容器在 `Loaded` 里按索引错峰入场（每张 9ms，最多 24 张，向上 10 DIP 淡入上浮），只在换页后 500ms 内生效，搜索重建容器不参与。磁贴悬停放大到 1.05（`Panel.ZIndex` 提到 20）、移出复位；文件夹浮层以 90% → 100% 的 `BackEase` 回弹弹出并让暗底淡入；页签选中时轻弹（1.09 → 1）。**整窗不播入场 / 退场动画**：打开直接显示，收起直接隐藏，换页与卡片动画不受影响。底行固定 `MinHeight="82"`，避免「＋」按钮在文件夹浮层里隐藏时状态提示上下跳。
- **布局与文件夹**：顺序和分组保存在 `data.json` 的顶层数组 `LauncherLayout`（每页 `Tiles`，普通条目用自然键 `app|路径` / `folder|路径` / `web|url`，文件夹用 `{Id,Name,Items[]}`），快捷方式本身仍以 `NativeAtomList` / `FolderAtomList` / `WebAtomList` 为唯一事实来源。打开时由 `LauncherLayout.Normalize` 对账：缺失条目剪掉、新增条目追加、空文件夹清理、单条目文件夹降级。搜索态与两个固定工具文件夹只读，不参与排序与分组；分组只在同一页内创建，不支持嵌套。`--shortcuts-check` 需继续通过，并覆盖可编辑判定、选择 API、文件夹展开/收起，以及浮层打开时底层页签卡片仍保留（`PageEntries`）。
- **开始菜单双向同步**：`StartMenuService` 负责读写 `开始菜单\程序`；启动器添加本地应用时自动在 `%APPDATA%\Microsoft\Windows\Start Menu\Programs\燧岚开发盒子\` 创建 `.lnk`，移除时只删这个受管目录里的文件，外部安装器已有的同目标快捷方式不重复创建也不删除。`NativeAtomList` 的 `StartMenuLink` 记录受管文件名、`StartMenuChecked` 记录是否已检查；旧数据在真实启动预热后由 `ShortcutService.EnsureStartMenuShortcuts` 后台补建一次（`--shortcuts-check` 探针提前返回，不会写开始菜单）。第四个页签「开始菜单」由 `StartMenuIndex` 递归读取用户与公共 `Programs`（用户覆盖公共），跳过受管目录与 `Startup`，按一级文件夹分组并以只读文件夹浮层展示；已经收进启动器的条目按解析目标 / `.lnk` 路径 / URL 隐藏，该页签只允许启动、添加到启动器与定位文件，不参与 `LauncherLayout`、框选、拖动与分组。开始菜单读取与图标提取在后台线程完成，结果缓存 5 分钟。
- **预热要落在最终几何**：`ApplyScreenBounds` 的差异比较必须同时处理 `NaN`（新窗口 `Left/Top/Width/Height` 是 `NaN`，`Math.Abs(NaN) > x` 恒 false，漏判会让窗口带着 `NaN` 显示、由 WPF 临时挑位置）；标准窗口不能靠根元素透明隐身，预热用 `Window.Opacity=0` 显示一次再隐藏，完成后恢复 `Opacity=1`。
- **启动时预热，不要按需创建**：`App.OnStartup` 在 `DispatcherPriority.ApplicationIdle` 调 `ShortcutWindowHost.Prewarm()`，以最终几何、`Window.Opacity=0`、`ShowActivated=false` 显示一次再隐藏，让 WPF 完成 HWND / 模板 / 布局 / 首帧；`ShowPage` 里另有保险，未预热就直接 `Show` 时会先补预热。按需创建会让“可见”发生在布局之前，表现为先闪空白再闪内容。
- **显示/激活只做一次**：`ShortcutWindowHost.Open` 不再补 `Show()` / `Activate()`，统一由 `ShowPage` 负责；激活用 `GetForegroundWindow()` 判断，只在确实不在前台时调用一次 `WindowPlacement.ForceForeground`；`Deactivated` 有 400ms 宽限，前台权限交接期间的假 Deactivated 不会把窗口关掉；另有 `OutsideClickDismissHook`（`WH_MOUSE_LL`）在可见期间监听窗口外点击，补上不触发 `Deactivated` 的点击场景（任务栏空白、窗口尚未激活等），与 Win 键钩子一样只在可见期间安装、隐藏后卸载。
- 数据仍走 `ShortcutService`；原先的 `AppsView` / `FoldersView` / `WebsView` 与对应 ViewModel **已删除**，不要再恢复卡片页。
- `DEVENV_START_PAGE=apps|folders|webs|startmenu|tools|builtin`、主窗拖放都经 `ShortcutWindowHost` 打开对应页签：`apps` / `folders` / `webs` / `startmenu` 切页签，`tools` / `builtin` 直接展开搜索框右侧对应的工具文件夹；`DEVENV_START_PAGE=devenv|lava|network|processes|services` 由 `App.NavigateToMainPage` 路由到开发 / 监控窗口。窗口单实例、非模态、`Topmost`，弹系统对话框前会临时取消置顶。
- 图标：`IconHelper.ExtractExecutableIcon` 优先提取 256px 大图标（启动器显示 48px），旧版 32px 缓存会在打开启动器时自动升级；`刷新图标` 会覆盖缓存。
- 自检：`DevEnv\bin\Release\net10.0-windows\DevEnv.exe --shortcuts-check %TEMP%\shortcuts.txt`，验证窗口加载、四个页签切换、开始菜单页只读、两个工具文件夹按钮与浮层展开、`DEVENV_START_PAGE=startmenu|tools|builtin` 路由、搜索与数据绑定错误（WPF 的 XAML / 绑定异常是静默的）。

### 主窗口（开发 / 监控两个）

- 原来一个主窗里的五个页面拆成两个窗口：**开发窗口**（开发环境 / 熔岩环境）与**监控窗口**（网络监控 / 进程监控 / 服务监控）。两者共用 `DevEnv/MainWindow.xaml(.cs)`，构造函数接收标题与 `IReadOnlyList<NavItem>`，`MainViewModel` 不再自带导航项。
- `ViewFactory.DevelopmentNavItems` / `MonitorNavItems` 是两套导航的唯一来源；新增主窗页面时按归属加进对应列表，`ViewFactory.IsMonitorPage` 决定 `HostCallbacks.OnActivateTab` 与 `DEVENV_START_PAGE` 路由到哪个窗口。
- 开发窗口在 `App.OnStartup` 创建并保持隐藏（`ShowInTaskbar = false`），监控窗口由 `EnsureMonitorWindow()` 按需创建：没人打开时不启动网络 / 进程监控页的定时器。托盘「显示主窗口」、`Ctrl+Alt+D`、二次启动只唤回开发窗口；监控窗口从全局启动器的「内置工具」页进入（也由 `HostCallbacks.OnShowMonitorWindow` 唤起）。
- 两个窗口关闭都只是隐藏到托盘；退出时 `ExitApp` 依次关闭两个窗口，全局热键钩子挂在开发窗口句柄上。翻译窗 / 剪贴板窗的 owner 取当前可见的宿主窗口（`VisibleMainWindow`），两个都隐藏时不挂。
- 自检：`DevEnv\bin\Release\net10.0-windows\DevEnv.exe --windows-check %TEMP%\windows.txt`，验证两个窗口各自加载、导航项与默认页正确、切页互不影响。

### 左 Alt 拦截

- 单独按一下左 Alt 键打开全局启动器，再按一次收起；`Alt+Tab` / `Alt+F4` / `Alt+空格` 等组合键原样放行。开关是 `data.json` 的 `Config.AltKeyLauncher`（默认 `true`），在「设置 → 快捷键」里可关。
- 判定逻辑在 `DevEnv.Core/Input/AltKeyGestureTracker.cs`（纯逻辑，单测覆盖）；钩子 `DevEnv/Infrastructure/AltKeyLauncherHook.cs` 只做 `WH_KEYBOARD_LL` 转发。
- **不吞任何按键**：左 Alt 的按下与抬起都原样交给系统，前台程序不会因为缺少抬起而把 Alt 误判成一直按着（这正是旧版 Win 拦截「按键异常」的根因）。代价是前台窗口的菜单栏会被点亮一下，属于系统原本的行为。
- **只认左 Alt**（`VK_LMENU`）：右 Alt（AltGr、语音键）永不触发；按住 Alt 期间按下其它键即视为组合键，本次手势不再触发；按住时的自动重复不算组合键。抬起可能报左 / 右 / 通用 Alt 三种码（`IsAltKey`），都用来结束手势，否则手势状态会残留、下一次单按左 Alt 不再触发。
- 钩子忽略注入按键（`LLKHF_INJECTED`），宏键盘 / 自动化工具模拟的按键不会弹全局启动器；只有自检探针通过 `AllowInjectedInput` 打开它。
- 模态对话框打开期间（`ShortcutWindowHost.CanOpen()` 为 false）不触发，让前台程序照常收到 Alt。
- **与开始菜单互斥**：开始菜单和启动器同为置顶窗口。老版本 Windows 由 `StartMenuExperienceHost` 承载，**新版 Windows 11（实测 build 26200）改由 `SearchHost` 承载**，窗口类仍是 `Windows.UI.Core.CoreWindow`、标题「搜索」；`ShellStartMenu` 两种宿主都要认，只认前者会在新系统上永远检测不到。打开启动器前 `ShortcutWindowHost.Open` 经 `ShellStartMenu.DismissIfOpen()` 给开始菜单发 Esc（开始菜单在前台时直接发，否则先 `SetForegroundWindow`），并延迟约 120ms 再显示；启动器可见期间 `WinKeyDismissHook` 监听真实 Win 键按下（只监听不拦截，开始菜单照常弹出），先把自己的窗口收起，避免开始菜单被压在启动器下面。Win 钩子只在可见期间安装，隐藏后卸载。
- 全局启动器**不设 Owner**：挂到主窗名下时，从托盘（主窗隐藏）唤起会把主窗一起拉出来；左 Alt 唤出时只允许出现启动器本身。
- 启动行为：`App.OnStartup` **不显示窗口**（开发窗口以 `ShowInTaskbar = false` 创建 + `EnsureHotkeys`，句柄由 `EnsureHandle` 提前创建；监控窗口更晚，按需创建），程序只驻留托盘；开发窗口由托盘菜单 / `Ctrl+Alt+D` / 二次启动唤出，监控窗口从全局启动器搜索框右侧「内置工具」文件夹里的「燧岚监控中心」进入。给窗口挂 Owner 前必须确认宿主窗口 `IsVisible`——owner 隐藏时 owned 窗口不会显示（翻译窗、剪贴板窗已按此处理，窗口显示时由 `AttachAuxiliaryWindows` 补挂）。
- 托盘入口：**单击托盘图标 = 全局启动器**（`TrayIconHost.MouseClick` → `ShortcutsRequested` → `ShortcutWindowHost.Toggle()`），与右键菜单「全局启动器…」或左 Alt 键一致；**设置**从右键菜单「设置…」（`SettingsRequested` → `SettingsDialogHost.Open()`）进入，关于与检查更新在设置的「关于与帮助」页；**熔岩翻译 / 熔岩剪贴板**从右键菜单进入，默认全局快捷键分别是 `Alt+Space`（`GlobalHotkeys.IdTranslator`）与 `Ctrl+Space`（`GlobalHotkeys.IdClipboard`），可在「设置 → 快捷键」修改；**开发窗口**只从右键菜单「显示主窗口」（`MainWindowRequested`）进入，单实例二次启动与 `Ctrl+Alt+D` 也唤回开发窗口；**监控窗口**从全局启动器「内置工具」页的「燧岚监控中心」进入。**快捷应用**（SigXor 的搜索框式启动器 `LauncherWindow`）已移除，不要再恢复；全局启动器是本仓库的 Launchpad 式悬浮窗口（`ShortcutsWindow`），代码里的历史类名保持不变。`ShortcutWindowHost.Toggle` 带 500ms 宽限：全局启动器因失焦收起后紧接着的这次调用按「关闭」处理。
- 与 SigXor 的 `GlobalHotkeyService` 是两个独立钩子，互不影响；退出路径必须先 `Dispose` 本钩子，避免钩子残留到进程退出之后。
- 自检：`DevEnv\bin\Release\net10.0-windows\DevEnv.exe --altkey-check %TEMP%\altkey.txt`，真实安装钩子并用 `SendInput` 模拟按键，验证「单按触发 / 组合键放行 / 右 Alt 不触发 / 卸载后失效」以及启动器 Win 键监听钩子的安装与卸载（不发送注入 Win 键，避免探针弹出开始菜单），其中「左 Alt 键状态已复位」一项用 `GetAsyncKeyState` 直接断言。

## 常用命令

```bash
make build                        # 构建全部（Release，= dotnet build DevEnv.sln -c Release）；产物在各项目的 bin/Release
make run                          # 以 Debug 配置编译并运行主程序（产物在 bin/Debug，避免锁住 make build 的 Release 输出）
make test                         # 跑单元测试（DevEnv.Tests，xunit，Release）
make verify                       # 校验 VERSION / csproj / CHANGELOG 三处一致
make publish                      # 框架依赖单文件发布到 publish/win-x64/（约 50 MB）
make package                      # 便携 app zip + 3 个组件 zip + latest.json → publish/release/
make docs                         # 文档站本地预览（vite dev server）
make deploy-docs                  # 文档站构建并部署到 Cloudflare Pages（仅本机执行，不走 CI）
```

调试主程序时可用环境变量直接启动到指定页：`DEVENV_START_PAGE=lava make run`（导航 id 见 `Views/ViewFactory.cs`；`devenv` / `lava` 走开发窗口，`network` / `processes` / `services` 走监控窗口，`apps` / `tools` 等走全局启动器）。

**编译检查与测试统一用 `make build` / `make test`，不要用 `make run` 代替**：`make build` 固定写入 Release 输出，`make run` 固定以 Debug 运行；正在运行的 `make run` 实例只会锁住 `bin/Debug`，不会让下一次 `make build` 因为 `bin/Release/DevEnv.Core.dll` 被占用而失败。只有确实要启动界面时才用 `make run`。

**发布固定用 `--no-self-contained`**：`.NET 10` SDK 在 `PublishSingleFile=true` 时会推断 `SelfContained=true`，而 `--self-contained false` 的空格写法不会被转发为显式 `false`，会把整个 Desktop Runtime 打进 exe（约 255 MB）。脚本、CI 与本机发布统一写 `--no-self-contained`。

## 硬性约定

1. **提交信息用中文**，格式为「`类型: 标题`」加详细正文（正文逐条写清改了什么、为什么、影响范围）。类型沿用仓库历史：`功能:` `修复:` `重构:` `性能:` `文档:` `样式:` `测试:` `杂项:`。
2. **提交信息里绝不能出现 AI 署名或生成标记**——`Co-Authored-By:`、`Generated with ...`、`Signed-off-by:` 一律不加，无论用哪个工具执行。工具默认会加，必须手动去掉。
3. **`Makefile` 必须保持纯 ASCII**。GNU Make 在 Windows 上会转码 makefile 里的非 ASCII 文本，UTF-8 注释和字符串在 `make help` 里会变成乱码。自检：`LC_ALL=C grep -c '[^ -~\t]' Makefile` 结果必须是 `0`。中文说明写到脚本或文档里，不要写进 Makefile。
4. **版本号有三处单一事实来源，必须同改**：`VERSION`、`DevEnv/DevEnv.csproj`（`Version` / `AssemblyVersion` / `FileVersion` / `InformationalVersion`，后两者形如 `X.Y.Z.0`）、`CHANGELOG.md` 里的 `## [x.y.z] - 日期`。改完跑 `make verify`；CI 会重复校验同一件事，不一致就构建失败。
5. **构建产物保留在各项目目录**。使用 .NET SDK 默认的 `<项目目录>/bin/`、`<项目目录>/obj/`，不要通过 `Directory.Build.props` 或 csproj 重定向到仓库根目录；仓库不保留 `global.json`，使用本机安装且支持 .NET 10 的 SDK。
6. **品牌、显示名与程序标识不是一回事**：品牌名为 **FireNeb**；界面文案 / `Product` / 托盘用「燧岚开发盒子」；英文名、程序集名、进程名、Release 资产前缀、用户数据目录固定为 `DevEnv`（`文档\FNSoftware\DevEnv`，产出 `DevEnv.exe`）。项目目录为 `DevEnv` / `DevEnv.Core`，命名空间为 `DevEnv.Host` / `DevEnv.Core`。旧版用户数据在 `文档\FNSoftware\DevBox` 时，启动时会迁入 `data.json` / `icons`。
7. **构建产物不入库**。`website/` 的 `.gitignore` 已忽略 `node_modules`、`dist` 等；`DevEnv.Core/LavaEnv/Resources/tools/7zr.exe` 是**故意入库**的例外（缺了它，CI 全新克隆构建出的安装包无法解压 7z / tar.gz）。
8. **全局热键跟随宿主生命周期**：主窗句柄就绪后注册，窗口关闭时释放；句柄重建时需 `Rebind` 补注册。
9. **改功能就同步文档**：模块级改动更新 `website/guide/` 下对应指南与 `CHANGELOG.md`；发版流程见 `website/guide/releases.md`。
10. **WPF 与 WinForms 同名类型必须显式别名**。`DevEnv.Host` 同时开了 `UseWPF` 与 `UseWindowsForms`（后者只为托盘与文件对话框），并已 `<Using Remove="System.Windows.Forms" />`；需要时写 `using Forms = System.Windows.Forms;`、`using WpfClipboard = System.Windows.Clipboard;`，**不要**裸写 `using System.Windows.Forms;`。
11. **颜色一律走主题资源**。界面颜色只能用 `Themes/Palette.*.xaml` 里的 key（控件样式已统一用 `DynamicResource`），不要在 XAML 里写死颜色，否则深色主题会露白。
12. **页面级 XAML 异常是静默的**。视图加载失败不会弹窗，只会整页不显示；排查先看 `%LOCALAPPDATA%\DevEnv\crash.log`。
13. **工具一律开独立非模态窗口**。`工具` 菜单下的各项走 `Infrastructure/ToolWindowHost`，不嵌入主窗内容区；新增工具时在 `ToolWindowHost.Tools` 注册即可，每工具只保留一个窗口实例。
14. **样式不能跨兄弟类型 `BasedOn`**。WPF 只允许 `BasedOn` 自己或自己的基类；`CheckBox` 与 `RadioButton` 互不为基类，公共部分必须抽到 `ToggleButton` 基样式上。
15. **服务层逻辑改动要配测试**。`DevEnv.Tests` 只引用 `DevEnv.Core`，测纯函数、序列化与文件/路径处理；测不了的东西（注册表、提权、进程、界面）不要硬写，要测就把逻辑抽成可注入的纯函数。涉及格式化的断言必须锁 `InvariantCulture`（见 `InvariantCultureScope`），否则换个语言环境就红。测试文件放在 `DevEnv.Tests/` 根目录，一个被测类型一个文件。
16. **文档站不用 VitePress**。`website/` 是 Vue 3 + Vite 官网：首页为 Vue 组件，指南正文继续维护在 `website/guide/**/*.md`，由 `src/docs/markdown.ts` 渲染。不要为了加文档重新引入 VitePress 或把正文改成 Vue 组件。
17. **用户数据与配置统一放在 `文档\FNSoftware\DevEnv\`**。宿主 `data.json`、熔岩环境配置与各模块配置都放这里，模块用子目录区分（如 `Translator/`、`Clipboard/`、`ContextMenu/`）；不要在用户主目录（如 `%USERPROFILE%\.lava_translator`）或 `文档\FNSoftware\` 下给功能模块另建独立目录。`文档\FNSoftware\SigXor`（文件预览 / 语音 / OCR 模块）与 `%LOCALAPPDATA%\DevEnv` 下的缓存、组件与更新产物是既有例外。
