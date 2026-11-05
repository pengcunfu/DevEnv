# Changelog

本文件记录各版本变更。发布新版本时请在此补充条目，并打 Git 标签（如 `v1.0.1`）触发 Release 工作流。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。

## [Unreleased]

### Added
- 合并 SigXor 独立应用到本仓库，作为「文件预览与语音」模块并入燧岚开发盒子：
  - **文件快速预览**：在资源管理器中选中文件按空格即可弹出 Quick Look 式浮窗，支持 13 种内容类型——纯文本、代码、Markdown、网页、CSV、SVG、字体、图片、音视频、PDF、Office（Word / Excel / PowerPoint）、压缩包、十六进制二进制；Hex 编辑器支持查找 / 跳转 / 修改 / 保存
  - **全局启动器**：任务栏聚焦时按空格唤起，可搜索并启动应用、打开文件、执行命令，支持固定常用项
  - **文件浏览器**：目录导航 + 内嵌预览面板，支持前后翻页、拖入文件直接预览
  - **语音输入**：基于 sherpa-onnx / SenseVoice 的本地语音转写，浮窗显示状态，结果可直接注入光标处
  - **区域截图与 OCR**：拖动选区截图，支持整页 OCR 取字、像素取色、复制与保存；OCR 模型本地管理
  - **命令行直接预览**：`DevEnv.exe <文件路径>` 打开预览浮窗；二次启动时经命名管道把路径转给主实例
- 新增 SigXor 模块自检探针 `--sigxor-check`（含 `--sigxor-smoke` / `--sigxor-preview-smoke` / `--sigxor-theme-smoke`）：自动验证 34 个窗口 / 视图能否加载、13 类预览内容能否渲染、深浅主题切换是否生效。WPF 的页面级 XAML 异常是静默的，靠肉眼点菜单不可靠
- 「工具」菜单与系统托盘新增文件预览相关入口：文件快速预览、全局启动器、文件浏览器、区域截图、语音与截屏设置、关于文件预览模块
- 新增 SigXor 合并脚本 `scripts/merge-sigxor.ps1`（搬运）、`scripts/rewrite-sigxor-namespace.ps1`（命名空间改写）、`scripts/verify-sigxor-merge.ps1`（逐字校验合并完整性），以及主题化脚本 `scripts/theme-sigxor-xaml.ps1` 与 `theme-sigxor-xaml-round2.ps1`

- 「开发环境」页改为**自定义后台服务**：服务列表由用户维护，支持添加 / 编辑 / 移除，每条可配置名称、工作目录、启动命令、停止命令与进程名；命令通过 `cmd /c` 执行，`npm run dev`、批处理与带参数的 exe 都能直接填，停止时优先执行停止命令、失败再结束进程树。配置存于 `data.json` 的 `DevServiceList`，取代原先写死的 MySQL / Redis / PostgreSQL 三项与本机绝对路径
- 开发环境服务支持**查看运行日志**：标准输出与错误输出实时进入非模态日志窗口（可跟随最新、清空、打开日志目录），同时写入 `文档\FNSoftware\DevEnv\logs\`，停止后与重启程序后都能回看；日志按行自动识别 UTF-8 与系统 ANSI 代码页（PostgreSQL 等按 GBK 输出的中文不再乱码），窗口顶对齐、自动折行并显示行号
- 新增「文件解锁与粉碎」工具（`工具` 菜单 → 独立窗口）：
  - **解除占用**：通过 Windows Restart Manager 检测占用文件 / 文件夹的进程，支持结束所选或全部可结束进程；关键系统进程与 DevEnv 自身受保护，普通权限下可一键请求管理员重启
  - **文件粉碎**：用随机数据覆盖文件 1 / 3 / 7 遍后截断、随机改名并删除，支持拖入文件与整个文件夹、逐文件进度与取消；单个文件失败不影响整批任务
  - 粉碎前做路径安全校验：拒绝磁盘根目录、Windows / Program Files / ProgramData / 用户主目录等系统目录、包含当前程序的目录，以及符号链接 / 目录联接
- 新增 `DevEnv.Tests` 单元测试工程（xunit 2.9 + Microsoft.NET.Test.Sdk）：只引用 `DevEnv.Core`，覆盖流量 / 字节格式化、文件与文本哈希、JSON 兼容字符串转换、Hosts 解析、网址规范化、剪贴板条目与时间戳、翻译站点目录、镜像源、熔岩默认设置，并校验随程序发布的 `processes_config.json` / `software_config.json` 本身可解析；`make test` 一键执行，Release 工作流在打包前先跑测试，测试不过不出安装包
- 集成 MenuManager 功能：新增「右键菜单管理」工具，可切换 Win11 右键菜单形态（一级 / 完整二级），并管理「用 XXX 打开」右键菜单项；与 MenuManager 共用 `文档\FNSoftware\MenuManager\menu_configs.json`，配置互通
- 「工具」菜单下的 JSON 格式化器 / Hosts 编辑器 / 图像转换器 / 哈希计算器 / 启动项管理 / 右键菜单管理改为**独立非模态窗口**，同一工具只保留一个窗口实例
- 深色主题：浅 / 深两套调色板 + 运行时切换，控件样式只写一份（颜色走 `DynamicResource`）
- 全局未处理异常落盘 `%LOCALAPPDATA%\DevEnv\crash.log`（桌面程序崩了用户看不到控制台）
- `DEVENV_START_PAGE` 环境变量：调试时直接启动到指定页
- 「进程监控」页：实时查看本机进程 CPU / 内存占用，支持搜索、按 CPU / 内存 / 名称排序、暂停刷新，以及结束进程、结束进程树、在资源管理器中定位
- 翻译 / 剪贴板改为独立窗口（翻译窗用 WebView2，剪贴板窗为纯 WPF）；系统菜单位于窗口菜单栏；熔岩环境下载分「活动 / 历史」页签

### Changed

- 快捷方式改为 **Launchpad 式全屏启动器**：「本地应用 / 文件夹 / 网站」从主窗左侧导航移出，主菜单「快捷方式」打开铺满屏幕的深色启动器——顶部搜索框（跨三类合并匹配，显示结果条数）、中部大图标网格（固定列宽、逐行居中）、底部横向页签；单击图标即启动，`Esc` 或点击空白处关闭，`←` / `→` 切页，`F5` 刷新，支持把 `.exe` / 文件夹直接拖入添加，右键图标可运行、定位、改名、刷新图标与移除。主窗左侧导航只保留开发环境、熔岩环境、网络监控、进程监控与系统工具，默认页改为「开发环境」；`DEVENV_START_PAGE=apps|folders|webs` 与「跳到页面」类入口统一改为打开启动器的对应页签。原先的卡片视图 `AppsView` / `FoldersView` / `WebsView` 及其 ViewModel 已删除，统一由启动器承载
- 应用图标改为优先提取 **256px 大图标**（`PrivateExtractIcons`，取不到时逐级回退到 48px / 系统默认 32px）；旧版按 32px 缓存的图标在首次打开启动器时自动重新提取，`刷新图标` 现在会真正覆盖缓存而不是被 `SaveIcon` 的「已存在即跳过」挡住
- 新增启动器自检探针 `--shortcuts-check [报告路径]`：加载窗口、遍历三个页签、跑一次搜索并收集 WPF 数据绑定错误，结果写报告文件；用法与 SigXor 自检探针一致
- **单独按一下 Win 键打开快捷方式启动台**，再按一次收起：新增 `WH_KEYBOARD_LL` 键盘钩子（`WinKeyLauncherHook`）与纯逻辑手势判定 `WinKeyGestureTracker`（10 个单测覆盖）；只吞 Win 键抬起、不吞按下，因此 `Win+E` / `Win+R` / `Win+D` / `Win+Tab` 等组合键完全不受影响，按住 Win 的自动重复、左右 Win 同时按、组合键后再单按都按预期处理；注入按键（宏键盘 / 自动化工具）不触发；模态对话框打开期间不抢 Win 键。开关为 `data.json` 的 `Config.WinKeyLauncher`（默认开启），可在「设置 → 常规」取消勾选恢复系统开始菜单，需要开始菜单时也可按 `Ctrl+Esc`
- 新增 Win 键拦截自检探针 `--winkey-check [报告路径]`：真实安装钩子并用 `SendInput` 模拟按键，验证单按触发、组合键放行、卸载后失效
- SigXor 的「文件预览与语音设置」独立模态窗口**已删除**，内容并入燧岚开发盒子的设置对话框，设置窗口改为**平铺三页签**（无二级页签）：「常规」= 主程序设置 + 文件预览的常规选项（字体 / 备份），「语音与截屏」，「服务与模型」；主菜单「设置」相应拆成「常规 / 语音与截屏 / 服务与模型」三项。原先散落在 SigXor 设置里的开机自启、托盘图标等全局选项去掉重复入口，统一在「常规」页签管理。设置窗口改为全局单实例（`SettingsDialogHost`），多个入口不再各弹一个框
- 设置对话框去掉嵌套结构：原「文件预览与语音」二级页签里的三个子页签（常规 / 语音与截屏 / 服务与模型）提升为顶层页签，与宿主「常规」页并列，不再有两层 TabControl；`SigXorSettingsView` 拆成 `SigXorGeneralView` / `SigXorVoiceView` / `SigXorServiceView` 三个独立视图，通过 `DevEnv.Core.ISettingsSections` 契约交给宿主注入
- 恢复熔岩运行时配置模板 `DevEnv.Core/LavaEnv/Resources/config.json`：该文件在镜像清单外置（提交 743c6eb）时一并丢失，导致首次运行无法从模板生成 `config.json`，`BundledConfigTests` 断言失败。模板内容取自原始版本，字段与 `AppSettings` 模型一致
- SigXor 合并后的命名空间与目录对齐：`DevEnv.Core/SigXor/` 下的纯逻辑层由 `DevEnv.Host.SigXor.Core` 改回 `DevEnv.Core.SigXor`，SigXor 单元测试命名空间由 `DevEnv.Host.SigXor.Tests` 改为 `DevEnv.Tests.SigXor`，符合「命名空间与目录一致」的约定；`rewrite-sigxor-namespace.ps1` 与 `verify-sigxor-merge.ps1` 的改写规则同步修正（裸名 `SigXor.Core;` 此前会被误改成 Host 命名空间）
- SigXor 并入后按宿主规范改造，删除其自带的第二套基础设施：独立 `App` / `Program` 入口、单实例服务、托盘服务、自研更新 / 回滚器（改用 DevEnv 的 Velopack 自更新，更新源与主程序一致）
- SigXor 的「开机自启」并入 DevEnv 设置里的唯一开关（`data.json` 的 `SelfStarting`，落盘到注册表 `HKCU\...\Run`），并清理旧版遗留的启动文件夹快捷方式，避免重复启动两次
- SigXor 模块配色统一走 `DynamicResource` 主题资源（110 处硬编码颜色已替换），深色主题下不再露白；截图 / 取色 / 选区 / 语音浮窗的功能性黑白配色保持不变
- SigXor 语法高亮沿用 VS Code Dark 配色（固定深色方案），代码编辑区底色相应固定为深色，不随主题切换
- 文件浏览器的关闭行为由「隐藏到托盘」改为正常关闭（托盘统一由 DevEnv 提供），预览浮窗、启动器、设置、关于、更新窗口均按单实例复用
- 熔岩「镜像配置」的镜像清单改为外部文件 `文档\FNSoftware\DevEnv\mirrors_config.json`：首次运行从内置模板生成，可直接编辑，镜像页新增「编辑镜像清单」入口，保存后点「刷新」生效；清单解析失败时自动回退到内置模板。内置清单同步校正为可用地址（移除已停用的豆瓣 pip 源与 Azure 中国 NuGet 源，补充南京大学 pip 源）
- 「开发环境」页去掉标题、说明与环境根目录展示，顶部操作区改为紧凑工具条；`BaseEnvPath` 仍可在设置中修改。添加、编辑、启停与移除等操作结果统一显示在主窗底部状态栏，不再占用页面空间
- 「网络监控」「进程监控」「系统工具」三页同样去掉页面标题与说明，内容整体上移；网络监控的「管理员重启」并入流量页工具条，系统工具的搜索框移到页面顶部
- 「进程监控」的进程详情移入独立非模态窗口：双击列表行打开，窗口复用并支持手动刷新，页面列表恢复整行宽度
- 「网络监控」的连接详情同样移入独立非模态窗口：双击流量页进程行打开，窗口复用并支持手动刷新；流量列表恢复整行宽度并补上可执行路径列
- 「网络监控」「进程监控」的快照采集改到线程池执行，界面只做数据绑定：进程枚举、连接枚举与 TCP 统计不再阻塞 UI 线程，两个页面的导航切换明显跟手；端口页签与详情窗口的手动刷新一并异步化
- 网络监控底部的状态卡并入主窗状态栏，显示连接数、上下行速率与提示，离开页面时自动清空
- 全应用取消加粗文字：移除主题里的半粗字重（页面标题、小节标题、选中的页签与列表项）以及各视图里显式写的 `FontWeight`。此前选中页签会把半粗字重传给整块内容，网络监控、熔岩环境的表格文字因此看起来发粗
- 左侧导航增加图标：本地应用、文件夹、网站、开发环境、熔岩环境、网络监控、进程监控、系统工具各配一个 Segoe MDL2 Assets 字形，通过 `NavItem.Glyph` 与 `ListBox.ItemTemplate` 渲染
- 熔岩剪贴板：「还原到剪贴板」改为「复制」；复制与删除不再占底部按钮栏，改为左侧列表与右侧详情共用的右键菜单，没有选中记录时置灰
- 熔岩剪贴板清空记录改为菜单：只保留「清空文本记录」「清空图片记录」两项，每项下再选全部 / 今天 / 昨天 / 前天（与三天保留窗口一致），不再单列「清空全部记录」；清空图片时同步删除图片文件
- 熔岩剪贴板窗口底部新增状态栏：左侧常驻显示条数统计（总 / 文本 / 图片 / 保留天数），右侧显示最近一次操作反馈（复制、删除、清空、导出），4 秒后自动隐藏；清空 0 条时改为提示「没有符合条件的记录」
- 熔岩剪贴板右侧详情只保留预览内容：去掉时间戳与「选择一条记录查看详情」的空态提示，未选中记录时保持空白；文本框取消外边距并铺满整张卡片，单行文本也保持整卡高度
- 熔岩剪贴板历史只保留最近 3 天：列表与导出按 3 天窗口查询，启动时及运行中每小时清理一次，删除更早的数据库记录和对应图片文件，避免无休止保存导致磁盘占用增长
- 产品中文显示名由「程序盒子」更名为「燧岚开发盒子」，品牌名 **FireNeb**；英文名、`DevEnv.exe`、Velopack `APP_ID` 与用户数据目录保持不变。官网与文档站同步更新品牌、域名与友链（FireNeb / Sigxor），开机自启注册表项在下次切换时自动清理旧名称
- 开源协议由 MIT 变更为 **Apache License 2.0**：根目录 `LICENSE` 换为 Apache 2.0 全文，新增 `NOTICE` 承载版权与归属声明；NuGet `PackageLicenseExpression`、README 与官网相关文案同步更新。此前已发布的版本仍按当时的 MIT 协议授权
- 仓库地址由 `github.com/pengcunfu/DevBox.Net` 更名为 `github.com/pengcunfu/DevEnv`：README、官网文档、`RepositoryUrl` 与自更新源（`UpdateService` / `AppInfo`）同步更新
- 构建产物恢复到各项目自己的 `bin/`、`obj/` 目录，移除根目录的路径重定向配置 `Directory.Build.props`；`make clean` 同步清理各项目产物与旧根目录残留
- 移除根目录 `global.json`，不再固定 .NET SDK 版本，使用本机安装的 SDK（构建仍需支持 .NET 10）
- **工程统一更名为 DevEnv**：`DevBox.Host/` → `DevEnv/`（工程 `DevEnv.csproj`）、`DevBox.Core/` → `DevEnv.Core/`；命名空间同步为 `DevEnv.Host.*` / `DevEnv.Core.*`。程序集名、进程名、Velopack `APP_ID` 与用户数据目录本就是 `DevEnv`，旧版 `文档\FNSoftware\DevBox` 的数据迁移逻辑不变
- **目录结构整合**：`Clipboard` / `NetworkWatch` / `ProcessWatch` / `Translator` / `LavaEnv` / `MenuManager` 六个独立工程并入 `DevEnv.Core` 与 `DevEnv.Host`，解决方案只保留这两个工程；模块命名空间统一为 `DevEnv.Core.<模块>.*`，配置与用户数据路径保持不变
- **技术栈切换为原生 WPF**：主界面、设置、系统工具、熔岩、网络 / 进程监控、剪贴板全部改为 WPF 原生界面（CommunityToolkit.Mvvm + 两套主题，浅色 / 深色）
- `DevBox.Api` 重命名为 `DevEnv.Core`，从「本机 Minimal API `:35174`」改为**进程内直调的应用服务层**（26 个 `*Endpoints` → 12 个服务类 + 强类型模型）；不再起 HTTP 服务、不再有会话 token 与 `wwwroot`
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
- 默认入口改为 `make run`（Host）；版本号与发布脚本指向 `DevEnv/DevEnv.csproj`
- 文档站 CI / `make docs` 改指文档站目录
- 逻辑库统一迁至 `libs/`（`Clipboard` / `DevEnv` / `NetworkWatch` / `ProcessWatch` / `Translator`）
- `make run` 改为「即时运行」：直接调用 `dotnet run --project DevEnv/DevEnv.csproj -c Release`，有改动时增量编译后启动；不再要求先 `make build`，并去掉原先的产物存在性检查与 `--no-build`

### Fixed

- 修复托盘图标单击无反应：此前只绑定了 `DoubleClick`，单击图标什么也不会发生。现在单击托盘图标即显示 / 唤回主窗口（右键仍弹出菜单），并强制置前拿到焦点；托盘事件统一经 `Dispatcher` 回 UI 线程处理
- 修复镜像配置的遗留状态：pip 切回官方源时清理旧的 `trusted-host`；Maven 只替换 Lava 托管的 mirror（旧版本按域名生成的条目一并清理），不再整份覆盖 `settings.xml`；NuGet 切换镜像时清掉上一轮的 `Lava-*` 源，恢复官方源改用 update / enable，避免先删后加失败
- 修复下拉框选中项显示成对象类型名（如 `DevEnv.Core.Translator.Models.TranslateSiteInfo`）：自定义 `ComboBox` 模板的选中项 `ContentPresenter` 漏了 `ContentTemplateSelector`，而 `DisplayMemberPath` 在 WPF 里是通过 `ItemTemplateSelector` 生效的，`SelectionBoxItemTemplate` 此时为 `null`。补上该绑定后，默认翻译引擎、软件版本、镜像工具等下拉框恢复显示正常名称
- 修复程序化导航重复创建页面：`Navigate` 同步左侧导航高亮时触发了 `OnCurrentNavChanged` 再次导航，`DEVENV_START_PAGE`、托盘唤回等入口会创建两份同一页面。旧实例没有进入可视树，`Unloaded` 不触发，其定时器会一直在后台采样；现在用 `_syncingNav` 守卫，含网络监控、进程监控在内的页面切换只创建一份视图
- 修复网络监控里选中进程或双击查看详情时程序直接退出：`TabControl` 的 `SelectionChanged` 会被内部 `DataGrid` 的选中事件冒泡触发，切页签逻辑重刷列表又反过来改选中项，形成无限递归并栈溢出；栈溢出无法被托管异常处理器捕获，所以 `crash.log` 与事件日志里都没有记录。现在只处理 `OriginalSource` 为 `TabControl` 自身的事件，熔岩环境页的同类写法一并加上护栏
- 双击详情改用 `PreviewMouseLeftButtonDown` 判断 `ClickCount`：`DataGridCell` 会先把普通双击事件处理掉，挂在 `MouseDoubleClick` 上不会触发
- 网络监控不再在 `NetworkService` 构造时另起一条后台快照轮询，改为只由页面定时器采集，避免两条线程同时读写采样字典
- 未处理异常日志补齐后台线程来源：`AppDomain.UnhandledException` 与 `TaskScheduler.UnobservedTaskException` 也会写入 `%LOCALAPPDATA%\DevEnv\crash.log`，排查静默退出时不再只有 UI 线程记录
- 添加网站时粘贴 `ftp://` / `file://` 等非 http(s) 地址不再被拼成 `https://ftp//…` 这类假地址，改为直接判定为无效网址
- 单文件发布时 `Resources/tools/7zr.exe` 与 `icon.ico` 会被打进 exe bundle，导致安装包内解压 7z / tar.gz 失败；这些资源现标记为排除单文件打包，稳定输出到 `Resources/`

### Removed

- 移除文档站部署工作流 `.github/workflows/docs.yml`：文档站不再由 GitHub Actions 发布，改由本机 `make deploy-docs`（或 `website/` 下 `npm run deploy`）构建并部署到 Cloudflare Pages
- 移除本机 HTTP API：`:35174` 端口、会话 token 鉴权、`wwwroot` 静态资源托管（图片不再经 HTTP，直接绑本地文件）
- 移除主壳 WebView2：界面不再嵌入 Chromium，只有熔岩翻译窗保留 WebView2（DOM 填充需要真实浏览器）
- 移除 Avalonia 主程序 `DevEnv/` 与共享控件库 `Lava.UI/`
- 内嵌模块迁至 `libs/`，并删除 Views / UI / `*.axaml` 等停用界面源文件；只保留供服务层 / Host 使用的 Services / Models
- 删除未引用的独立项目 `WinBootSelfStarting/`（启动项已由 `DevEnv.Core` 承接）与对应过时文档
- 删除独立工程 `Clipboard` / `NetworkWatch` / `ProcessWatch` / `Translator` / `LavaEnv` 与独立应用 `MenuManager`（含其独立 sln、About 对话框）；功能已并入 `DevEnv.Core` 与 `DevEnv.Host`，右键菜单配置路径仍为 `文档\FNSoftware\MenuManager\menu_configs.json`

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

[Unreleased]: https://github.com/pengcunfu/DevEnv/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/pengcunfu/DevEnv/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/pengcunfu/DevEnv/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/pengcunfu/DevEnv/releases/tag/v1.0.0
