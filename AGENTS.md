# AGENTS.md

给在本仓库工作的 AI 编码助手（Claude Code / Cursor / Codex 等）的说明，人类贡献者同样适用。
写代码前先读这一页；与 `Makefile`、`scripts/verify-version.ps1` 冲突时，以脚本的实际校验逻辑为准。

## 项目是什么

**DevEnv**（产品显示名「燧岚开发盒子」，品牌 FireNeb）是 Windows 桌面工具：.NET 10 + 原生 WPF 界面，把开发环境管理和几个自研小工具收进同一个壳。只有熔岩翻译窗内嵌 WebView2（DOM 填充需要真实浏览器）。解决方案文件是 `DevEnv.sln`。

| 路径 | 角色 |
|------|------|
| `DevEnv/` | 主程序（`WinExe`，WPF）：全部界面、托盘、全局热键、剪贴板轮询、Velopack 安装与自更新；**程序集名 / 进程名为 `DevEnv`**（产出 `DevEnv.exe`） |
| `DevEnv.Core/` | 应用服务层（普通类库）：数据持久化，以及熔岩 / 网络 / 进程 / 剪贴板 / 翻译 / 右键菜单 / 更新等业务编排，按功能分 `LavaEnv/`、`NetworkWatch/`、`ProcessWatch/`、`Clipboard/`、`Translator/` 子目录；界面**进程内直调**，没有 HTTP |
| `DevEnv.Tests/` | 单元测试工程（xunit 2.9 + Microsoft.NET.Test.Sdk）：只引用 `DevEnv.Core`，覆盖纯逻辑、序列化与文件/路径处理；`make test` 执行，CI 发版前也会跑一遍 |
| `website/` | Vue 3 + Vite 官网与文档站（首页 + `guide/**/*.md` 文档 + `/downloads` 软件下载站，正文由 markdown-it 渲染；下载站数据在 `src/data/software.json`）→ Cloudflare Pages，项目名 `devbox-docs`，站点 https://devenv.cn/ |
| `docs/` | 子项目说明文档（纯 Markdown，**不是**文档站，别往里放构建配置） |
| `scripts/` | 发布与版本校验的 PowerShell 脚本 |

**功能模块是 `DevEnv.Core` / `DevEnv` 工程内的子目录，不再有独立工程**。新功能按 `DevEnv.Core/<模块>/{Models,Services}` + `DevEnv/{Views,ViewModels,Windows}` 放置，命名空间与目录一致（`DevEnv.Core.<模块>.*` / `DevEnv.Host.*`）。不要恢复独立 `Program.cs`、独立 `sln` 或独立入口。

### SigXor 模块（文件快速预览 / 语音输入 / 截图 OCR 取色）

原 SigXor 独立应用已并入本仓库，代码保留原结构但**命名空间与目录已改写**：

| 位置 | 内容 |
|------|------|
| `DevEnv.Core/SigXor/` | 纯逻辑：HexDocument、文件类型识别、编码检测、Office / RTF / ODF 解析、CSV 解析、Markdown 渲染、快捷键与屏幕几何。命名空间 `DevEnv.Core.SigXor.*` |
| `DevEnv/SigXor/` | WPF 界面与系统能力：预览浮窗、语音输入、截图 / OCR / 取色、快捷应用、文件浏览器。命名空间 `DevEnv.Host.SigXor.*` |
| `DevEnv/SigXor/SigXorHost.cs` | **模块装配入口**。原 SigXor 的 `App` 已改造为此类：不再继承 `Application`、不再自带托盘 / 单实例 / 独立入口（这些统一由 `DevEnv.Host.App` 提供），只保留模块自己的接线与生命周期 |
| `DevEnv/SigXor/SigXorStyles.xaml` | 原 `App.xaml` 里的 `EngineToggle` 样式，已改为 `ResourceDictionary` 并入主 `App.xaml` |
| `DevEnv.Tests/` 根目录 | SigXor 的 12 个测试文件（`namespace DevEnv.Tests.SigXor`），与 DevEnv 原有测试同工程 |

约定：

- **不要再引入第二套基础设施**。托盘、单实例、自启动、更新、异常日志一律用 DevEnv 现有的（`TrayIconHost`、`SingleInstance`、`AutostartHelper`、Velopack `UpdateService`）。SigXor 自带的 `TrayIconService` / `SingleInstanceService` / `Updater` / `App` / `Program` 是未接线的遗留代码，**已删除**，不要接回去。
- 模块要暴露能力给宿主，走 `DevEnv.Core.HostCallbacks`（`OnOpenFilePreview` / `OnOpenScreenshot` / `OnOpenSigXorSettings` / `OnOpenSigXorLauncher` / `OnOpenSigXorBrowser` / `OnOpenSigXorUpdate`），不要在 Core 里直接碰 UI。
- SigXor 的数据目录仍是 `文档\FNSoftware\SigXor`（与 DevEnv 的 `文档\FNSoftware\DevEnv` 并存），旧版 SigXor 配置与模型在首次启动时自动迁移。
- 全局键盘钩子：SigXor 用自己的 `GlobalHotkeyService`（`WH_KEYBOARD_LL`，处理预览键 / 关闭键 / 语音键 / 反引号），与 DevEnv 的 `RegisterHotKey` 体系**并存但不冲突**（前者管预览与语音，后者管显示窗口与快捷应用）。多个 SigXor 功能共用一个钩子时靠 `GlobalHotkeyRouter` 的**优先级**排序：截图 300 > 语音 200 > 预览 100。快捷键录制期间 `HotkeyCaptureState.IsCapturing` 为 true，路由直接放行按键，避免录制时触发截图 / 语音 / 预览。
- **开机自启只有一个开关**：DevEnv「设置 → 常规」里的 `SelfStarting`（data.json）是唯一事实来源，落盘到注册表 `HKCU\...\Run`。SigXor 的设置页里**不再有**同名勾选框（已并入宿主设置对话框），需要改自启就去宿主的「常规」页签。
- **设置统一在宿主设置对话框里**：SigXor 原独立设置窗口（`DevEnv/SigXor/SettingsWindow.*`）**已删除**，内容拆成四个视图（`DevEnv/SigXor/Views/SigXorGeneralView.xaml` / `SigXorVoiceView.xaml` / `SigXorServiceView.xaml` 与宿主 `DevEnv/Views/HotkeysSettingsView.xaml`），并入燧岚开发盒子设置对话框的**平铺五页签**（**不要二级页签**）：「常规」= 主程序设置 + 文件预览的常规选项，「快捷键」= 所有程序绑定的快捷键，「语音与截屏」，「服务与模型」，「关于与帮助」= 产品信息 + 检查更新 + 帮助文档。中间三页签的内容由 `SettingsDialogHost.Sections`（`ISettingsSections`）在运行时注入（`SigXorHost.CreateSettingsSections`），「关于与帮助」由宿主 `DevEnv/Views/AboutHelpView.xaml` 提供；不要再造第二个模态设置框。主窗口顶部**不再有「设置」「帮助」菜单**，设置统一从托盘右键菜单「设置…」（`TrayIconHost.SettingsRequested` → `SettingsDialogHost.Open()`）进入。
- **快捷键统一在「设置 → 快捷键」**：显示主窗口 / 快捷应用由 `GlobalHotkeys` 通过 `RegisterHotKey` 注册（`data.json` 的 `Config.ShowWindowHotkey` / `Config.QuickAppHotkey`）；Win 键启动器、语音键、截屏组合、文件预览打开 / 关闭键也都在该页修改。全局组合键用 `HotkeyRecorderBox` 录制，录制期间 `HotkeyCaptureState` 会挂起 `GlobalHotkeys` 与 `WinKeyLauncherHook`；常规页保存走 `SettingsService.SaveGeneral`，快捷键页走 `SaveHotkeys`，两者不得互相覆盖。
- **自检探针**：SigXor 有 34 个 XAML（11 个窗口 + 15 个预览视图 + 7 个 WebView2 宿主），而 WPF 的 XAML 异常是静默的（AGENTS.md 第 12 条），靠肉眼点菜单不可靠。改动模块后跑：

 ```bash
 DevEnv\bin\Release\net10.0-windows\DevEnv.exe --sigxor-check %TEMP%\sigxor.txt
 ```

 三组探针：窗口 / 视图能否加载、预览内容能否渲染、深浅主题切换是否生效。单个探针也有独立开关：`--sigxor-smoke` / `--sigxor-preview-smoke` / `--sigxor-theme-smoke`。合并设置对话框后另有 `--settings-check`：验证设置窗口平铺的「常规」「快捷键」「语音与截屏」「服务与模型」「关于与帮助」五个页签都能加载并切换。
- **预览内容类型共 13 种**（`ContentKind`）：Text / Code / Image / Svg / Archive / Csv / Font / Document / Markdown / WebPage / Audio / Video / Pdf / Binary。新增类型要同时改 `DevEnv.Core/SigXor/FileTypeDetector.cs` 与 `DevEnv/SigXor/ViewModels/PreviewViewModel.cs` 的分发，并补一条预览探针用例。
- **webassets 是嵌入 exe 的资源**（`EmbeddedResource` + `LogicalName="webassets/..."`），运行时由 `DevEnv/SigXor/WebAssets.cs` 按内容指纹解压到 `%LOCALAPPDATA%\SigXor\WebAssets\{指纹}`。**不要**改成复制到输出目录——WebView2 的虚拟主机映射只接受真实目录。
- **语法高亮配色跟随主题**（`SyntaxHighlightingService`）：深色主题用 VS Code Dark，浅色主题用 VS Code Light，`ThemeManager.ThemeChanged` 触发后重新注册定义，已打开的 `TextView` 重新取一次定义并刷新。`TextView` 的编辑器底色 / 前景 / 行号颜色必须走 `DynamicResource`（`SurfaceBrush` / `TextPrimaryBrush` / `TextMutedBrush`），**不要写死深色**，否则浅色主题下会是一块黑底。其余界面颜色同样一律走 `DynamicResource`，截图 / 取色 / 选区 / 语音浮窗的功能性黑白配色除外（见上一条）。
- **截图 / 取色 / 选区 / 语音浮窗里的黑白配色是功能性颜色**，必须保持不透明与高对比，不随主题变化（`scripts/theme-sigxor-xaml.ps1` 已把它们列为例外）。
- **合并脚本**：`scripts/merge-sigxor.ps1`（搬运）+ `scripts/rewrite-sigxor-namespace.ps1`（改写命名空间）+ `scripts/verify-sigxor-merge.ps1`（逐字校验）。合并 SigXor 新版本后跑一次 verify，确认「除预期的宿主适配外与源仓库逐字一致」。

### 全局启动器（Launchpad）

- 入口：单独按 Win 键或托盘右键菜单「全局启动器…」 → `ShortcutWindowHost.Open()` → `DevEnv/Windows/ShortcutsWindow.xaml(.cs)`，铺满主屏的深色启动器，本地应用 / 文件夹 / 网站 / 系统工具是四个**横向页签**（`ShortcutLauncherViewModel`）。系统工具页复用 `ShortcutService.GetSystemTools / RunSystemTool`，主窗不再保留「系统工具」导航页。代码里的 `ShortcutsWindow` / `ShortcutWindowHost` 是历史类名，界面与文档一律称「全局启动器」。
- 交互：单击图标启动、`Esc` 关闭、`←` / `→` 或 `Ctrl` + 鼠标滚轮切页、`F5` 刷新、拖入 `.exe` / 文件夹添加；右键图标可运行、定位、改名、刷新图标、移除。**点击空白处不再关闭窗口**，而是开始框选；拖动空白绘制选框多选（`Ctrl` + 单击切换选中），拖动卡片调整顺序，把卡片拖到另一个卡片的图标中心合并成启动器文件夹，点击文件夹磁贴展开。滚轮监听在根 Grid 的 `PreviewMouseWheel`（滚动内容会被 `ScrollViewer` 标记已处理，冒泡阶段收不到），搜索期间与左右键一样不切页。
- **布局与文件夹**：顺序和分组保存在 `data.json` 的顶层数组 `LauncherLayout`（每页 `Tiles`，普通条目用自然键 `app|路径` / `folder|路径` / `web|url`，文件夹用 `{Id,Name,Items[]}`），快捷方式本身仍以 `NativeAtomList` / `FolderAtomList` / `WebAtomList` 为唯一事实来源。打开时由 `LauncherLayout.Normalize` 对账：缺失条目剪掉、新增条目追加、空文件夹清理、单条目文件夹降级。系统工具页与搜索态只读，不参与排序与分组；分组只在同一页内创建，不支持嵌套。`--shortcuts-check` 需继续通过，并覆盖可编辑判定、选择 API 与文件夹展开/返回。
- **预热要落在最终几何、内容保持全透明**：`ApplyScreenBounds` 的差异比较必须同时处理 `NaN`（新窗口 `Left/Top/Width/Height` 是 `NaN`，`Math.Abs(NaN) > x` 恒 false，漏判会让窗口带着 `NaN` 显示、由 WPF 临时挑位置）；**收起必须先淡出、后 `Hide()`**：分层窗口缓存最后一次合成结果，先 `Hide()` 再设透明是无效的（不可见就不会再渲染），下一次 `Show` 会把不透明缓存当作第一帧贴出来——表现为「第一次打开正常、之后每次都闪」。现在的顺序是 `Root.Opacity` 动画到 0 → `Completed` 里 `Hide()`；`IsHiding` 期间再次唤起会取消收起。
- **启动时预热，不要按需创建**：`App.OnStartup` 在 `DispatcherPriority.ApplicationIdle` 调 `ShortcutWindowHost.Prewarm()`，窗口在屏幕外（-20000）、`Opacity=0`、`ShowActivated=false` 下显示一次再隐藏，让 WPF 完成 HWND / 模板 / 布局 / 首帧；`ShowPage` 里另有保险，未预热就直接 `Show` 时会先补预热。按需创建会让“可见”发生在布局之前，表现为先闪空白再闪内容。
- **显示/激活只做一次**：`ShortcutWindowHost.Open` 不再补 `Show()` / `Activate()`，统一由 `ShowPage` 负责；淡入动画做在根元素 `Root.Opacity` 上而不是 `Window.Opacity`（`AllowsTransparency` 的分层窗口首次显示会先画一帧不透明，再被窗口透明度拉回 0，看起来就是闪两下）；激活用 `GetForegroundWindow()` 判断，只在确实不在前台时调用一次 `WindowPlacement.ForceForeground`；`Deactivated` 有 400ms 宽限，前台权限交接期间的假 Deactivated 不会把窗口关掉。
- 数据仍走 `ShortcutService`；原先的 `AppsView` / `FoldersView` / `WebsView` 与对应 ViewModel **已删除**，不要再恢复卡片页。
- `DEVENV_START_PAGE=apps|folders|webs|tools`、主窗拖放都经 `ShortcutWindowHost` 打开对应页签；窗口单实例、非模态、`Topmost`，弹系统对话框前会临时取消置顶。
- 图标：`IconHelper.ExtractExecutableIcon` 优先提取 256px 大图标（启动器显示 56px），旧版 32px 缓存会在打开启动器时自动升级；`刷新图标` 会覆盖缓存。
- 自检：`DevEnv\bin\Release\net10.0-windows\DevEnv.exe --shortcuts-check %TEMP%\shortcuts.txt`，验证窗口加载、四个页签切换、系统工具目录加载、搜索与数据绑定错误（WPF 的 XAML / 绑定异常是静默的）。

### Win 键拦截

- 单独按一下 Win 键打开全局启动器，再按一次收起；`Win+E` / `Win+R` / `Win+D` 等组合键原样放行。开关是 `data.json` 的 `Config.WinKeyLauncher`（默认 `true`），在「设置 → 常规」里可关。
- 判定逻辑在 `DevEnv.Core/Input/WinKeyGestureTracker.cs`（纯逻辑，单测覆盖）；钩子 `DevEnv/Infrastructure/WinKeyLauncherHook.cs` 只做 `WH_KEYBOARD_LL` 转发。
- **只吞 Win 键抬起，不吞按下**：开始菜单是抬起时弹出的，吞抬起即可挡住它；吞按下会让 Win+组合键全部失效，所以按下阶段必须放行。按住 Win 的自动重复、左右 Win 同时按都已单独处理。
- **吞掉物理抬起后必须补发「假按键 + Win 抬起」**（`ReleaseWinKey`）：系统看到 Win 按下却收不到抬起，会一直认为 Win 修饰键按着，之后任何按键都变成 Win+组合键——这正是「Win 接管后按键异常」的根因。补发未分配虚拟键 `0xE8` 是为了打断开始菜单的「单击 Win」判定，再补发同侧 Win 抬起让按键状态复位。改动这段务必跑 `--winkey-check`，其中「Win 键状态已复位」一项用 `GetAsyncKeyState` 直接断言。
- 钩子忽略注入按键（`LLKHF_INJECTED`），宏键盘 / 自动化工具模拟的 Win 键不会弹全局启动器；只有自检探针通过 `AllowInjectedInput` 打开它。
- 模态对话框打开期间（`ShortcutWindowHost.CanOpen()` 为 false）不吞 Win 键，让系统开始菜单照常弹出。
- 全局启动器**不设 Owner**：挂到主窗名下时，从托盘（主窗隐藏）唤起会把主窗一起拉出来；Win 键唤出时只允许出现启动器本身。
- 启动行为：`App.OnStartup` **不显示主窗**（`new MainWindow { ShowInTaskbar = false }` + `EnsureHotkeys`，句柄由 `EnsureHandle` 提前创建），程序只驻留托盘；主窗由托盘菜单 / `Ctrl+Alt+D` / 二次启动唤出。给窗口挂 Owner 前必须确认主窗 `IsVisible`——owner 隐藏时 owned 窗口不会显示（翻译窗、剪贴板窗已按此处理，主窗显示时由 `AttachAuxiliaryWindows` 补挂）。
- 托盘入口：**单击托盘图标 = 快捷应用**（`TrayIconHost.MouseClick` → `SigXorLauncherRequested`，搜索并启动应用 / 文件 / 命令），全局快捷键默认 `Alt+Space`（`GlobalHotkeys.IdLauncher`，可在「设置 → 快捷键」修改）；**设置**从右键菜单「设置…」（`SettingsRequested` → `SettingsDialogHost.Open()`）进入，关于与检查更新在设置的「关于与帮助」页；**全局启动器**从右键菜单「全局启动器…」（`ShortcutsRequested` → `ShortcutWindowHost.Toggle()`）或 Win 键进入；**主窗口**只从右键菜单「显示主窗口」（`MainWindowRequested`）进入，单实例二次启动与 `Ctrl+Alt+D` 仍直接唤回主窗口。注意区分两个启动器：**快捷应用**是 SigXor 的搜索框式启动器（`LauncherWindow`，代码里仍叫 launcher），**全局启动器**是本仓库的 Launchpad 式全屏窗口（`ShortcutsWindow`）。`ShortcutWindowHost.Toggle` 带 500ms 宽限：全局启动器因失焦收起后紧接着的这次调用按「关闭」处理。
- 与 SigXor 的 `GlobalHotkeyService` 是两个独立钩子，互不影响；退出路径必须先 `Dispose` 本钩子，否则进程退出后开始菜单仍会被吞。
- 自检：`DevEnv\bin\Release\net10.0-windows\DevEnv.exe --winkey-check %TEMP%\winkey.txt`，真实安装钩子并用 `SendInput` 模拟按键，验证「单按触发 / 组合键放行 / 卸载后失效」。

## 常用命令

```bash
make build                        # 构建全部（Release，= dotnet build DevEnv.sln -c Release）；产物在各项目的 bin/Release
make run                          # 以 Debug 配置编译并运行主程序（产物在 bin/Debug，避免锁住 make build 的 Release 输出）
make test                         # 跑单元测试（DevEnv.Tests，xunit，Release）
make verify                       # 校验 VERSION / csproj / CHANGELOG 三处一致
make publish                      # 单文件发布到 publish/win-x64/
make installer                    # publish + vpk pack，产出 Velopack Setup.exe
make docs                         # 文档站本地预览（vite dev server）
make deploy-docs                  # 文档站构建并部署到 Cloudflare Pages（仅本机执行，不走 CI）
```

调试主程序时可用环境变量直接启动到指定页：`DEVENV_START_PAGE=lava make run`（导航 id 见 `Views/ViewFactory.cs`）。

**编译检查与测试统一用 `make build` / `make test`，不要用 `make run` 代替**：`make build` 固定写入 Release 输出，`make run` 固定以 Debug 运行；正在运行的 `make run` 实例只会锁住 `bin/Debug`，不会让下一次 `make build` 因为 `bin/Release/DevEnv.Core.dll` 被占用而失败。只有确实要启动界面时才用 `make run`。

## 硬性约定

1. **提交信息用中文**，格式为「`类型: 标题`」加详细正文（正文逐条写清改了什么、为什么、影响范围）。类型沿用仓库历史：`功能:` `修复:` `重构:` `性能:` `文档:` `样式:` `测试:` `杂项:`。
2. **提交信息里绝不能出现 AI 署名或生成标记**——`Co-Authored-By:`、`Generated with ...`、`Signed-off-by:` 一律不加，无论用哪个工具执行。工具默认会加，必须手动去掉。
3. **`Makefile` 必须保持纯 ASCII**。GNU Make 在 Windows 上会转码 makefile 里的非 ASCII 文本，UTF-8 注释和字符串在 `make help` 里会变成乱码。自检：`LC_ALL=C grep -c '[^ -~\t]' Makefile` 结果必须是 `0`。中文说明写到脚本或文档里，不要写进 Makefile。
4. **版本号有三处单一事实来源，必须同改**：`VERSION`、`DevEnv/DevEnv.csproj`（`Version` / `AssemblyVersion` / `FileVersion` / `InformationalVersion`，后两者形如 `X.Y.Z.0`）、`CHANGELOG.md` 里的 `## [x.y.z] - 日期`。改完跑 `make verify`；CI 会重复校验同一件事，不一致就构建失败。
5. **构建产物保留在各项目目录**。使用 .NET SDK 默认的 `<项目目录>/bin/`、`<项目目录>/obj/`，不要通过 `Directory.Build.props` 或 csproj 重定向到仓库根目录；仓库不保留 `global.json`，使用本机安装且支持 .NET 10 的 SDK。
6. **品牌、显示名与程序标识不是一回事**：品牌名为 **FireNeb**；界面文案 / `Product` / 托盘用「燧岚开发盒子」；英文名、程序集名、进程名、Velopack `APP_ID`、用户数据目录固定为 `DevEnv`（`文档\FNSoftware\DevEnv`，产出 `DevEnv.exe`）。项目目录为 `DevEnv` / `DevEnv.Core`，命名空间为 `DevEnv.Host` / `DevEnv.Core`。旧版用户数据在 `文档\FNSoftware\DevBox` 时，启动时会迁入 `data.json` / `icons`。
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
