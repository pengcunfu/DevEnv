# Changelog

本文件记录各版本变更。发布新版本时请在此补充条目，并打 Git 标签（如 `v1.0.1`）触发 Release 工作流。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。

## [Unreleased]

### Changed
- 全局启动器卡片图标从 56 × 56 缩到 48 × 48（图标底板 72 × 72 缩到 64 × 64），拖放合并热区同步调整为 `Rect(34, 13, 64, 64)`；「内置工具」里的 12 个入口改用各自的品牌色圆角图标（白色 Segoe MDL2 字形），不再统一显示灰色字形，文件夹磁贴的 2 × 2 预览同步变小
- 全局启动器改用 **Windows 默认窗口边框与标题栏**：不再自绘透明分层窗口，去掉打开时抓取桌面做磨砂、22 DIP 圆角裁剪与玻璃高光，窗口背景改为不透明；同时**移除整窗入场 / 退场动画**，打开直接显示、收起直接隐藏（换页滑入、卡片错峰与文件夹浮层动画保留）。预热改用 `Window.Opacity=0` 保证首帧不可见
- 全局启动器改为**固定 1200 × 800 DIP、水平垂直居中**；内容配色按系统的「应用模式」（`AppsUseLightTheme`）在浅 / 深两套之间切换，与开始菜单一致。顶部一行按新宽度调整页签内边距、搜索框（120–250 自适应）与工具文件夹按钮，操作提示从右上角移到底部居中；`DevEnv.Core/Services/LauncherGeometry.cs` 集中几何计算并补单元测试，`--shortcuts-check` 增加尺寸、居中与标准窗口检查
- 移除「快捷应用」界面（SigXor 搜索框式启动器 `LauncherWindow` / `LauncherService`）及其托盘菜单、系统工具目录和自检探针入口，托盘单击改为打开全局启动器；熔岩剪贴板与熔岩翻译改回全局热键：`Ctrl+Space` 打开剪贴板窗口，`Alt+Space` 打开翻译窗口并自动填充剪贴板文本。`data.json` 的 `Config.QuickAppHotkey` 由 `Config.ClipboardHotkey` 与 `Config.TranslatorHotkey` 取代，「设置 → 快捷键」页对应增加两个录制框；应用、文件、命令的启动能力继续由全局启动器提供
- 全局启动器页签扩为「本地应用 / 文件夹 / 网站 / 开始菜单」四个：第四个页签只读展示用户与公共开始菜单里的 `.lnk` / `.url`，按一级文件夹分组，已收进启动器的条目按目标路径 / URL 自动隐藏，右键可启动、定位或添加到启动器
- 用户数据与配置统一收拢到 `文档\FNSoftware\DevEnv\`：
  - 熔岩翻译配置从 `%USERPROFILE%\.lava_translator\config.json` 迁到 `文档\FNSoftware\DevEnv\Translator\config.json`，首次启动自动迁移并清理旧目录
  - 熔岩剪贴板数据从 `文档\FNSoftware\Clipboard\` 迁到 `文档\FNSoftware\DevEnv\Clipboard\`，首次启动自动迁移 `config.json`、`clipboard.db`、`clipboard_history.json` 与 `clipboard_images/`
  - 右键菜单管理配置从 `文档\FNSoftware\MenuManager\menu_configs.json` 迁到 `文档\FNSoftware\DevEnv\ContextMenu\menu_configs.json`，旧位置与程序目录下的配置会在首次启动时自动迁入
- 全局启动器顶部改为一行工具条：「本地应用 / 文件夹 / 网站」三页签、搜索框、系统工具与内置工具文件夹从左到右依次排列；搜索框宽度从 560 收窄到 280，底部只保留搜索结果条数与操作提示，页签在搜索态与文件夹浮层打开时隐藏但仍占位，搜索框不会向左跳
- 全局启动器把「系统工具」「内置工具」从底部页签移到搜索框右侧，作为两个只读文件夹按钮：点击后复用启动器文件夹浮层展开工具图标，页签减为「本地应用 / 文件夹 / 网站」三个。`DEVENV_START_PAGE=tools|builtin` 仍会直接展开对应文件夹，`--shortcuts-check` 增加工具文件夹按钮、浮层展开与路由检查

### Added
- 全局启动器与 Windows 开始菜单双向打通：往「本地应用」添加 `.exe` 时自动在 `开始菜单\程序\燧岚开发盒子\` 创建 `.lnk`，移除时只删除该受管目录里的快捷方式；已有启动器应用在下次启动预热后后台补建一次。外部安装器已有的同目标快捷方式不会重复创建，也不会被删除；托管文件名记录在 `data.json` 的 `StartMenuLink`
- 全局启动器「内置工具」文件夹新增「熔岩翻译」「熔岩剪贴板」「文件浏览器」三个入口：翻译沿用翻译热键行为（打开翻译窗并填充剪贴板文本），剪贴板打开历史窗口，文件浏览器打开目录导航与内嵌预览窗口
- 全局启动器的「内置工具」页签新增两个主窗口入口：**「燧岚开发盒子」**（开发环境 / 熔岩环境，key 为 `dev-window`）与 **「燧岚监控中心」**（网络 / 进程 / 服务监控，key 为 `monitor-window`），分别走 `HostCallbacks.OnShowDevelopmentWindow` / `OnShowMonitorWindow`
- 新增 `--windows-check` 自检探针：验证开发窗口与监控窗口各自加载、导航项与默认页正确、切页互不影响
- 文本预览支持 **`Ctrl` + 鼠标滚轮缩放字号**：范围 8 到 72，文本、Markdown 源码与网页源码共用同一个会话级字号；不按 Ctrl 时滚轮保持正常滚动，在设置里保存新的默认字号后缩放重置
- 空格预览支持**桌面**：在桌面上选中文件或文件夹后按空格即可预览，换选桌面图标后再按空格继续预览，浏览历史与左右切换同样适用。桌面不在 `Shell.Application.Windows()` 里，改为从 `SHELLDLL_DefView` 下的 `SysListView32` 读取选中项（`LVM_GETNEXTITEM` + `LVM_GETITEMTEXTW`，跨进程读写 `LVITEM`），并按桌面目录回查被省略的扩展名（如 `.lnk`）
- 空格预览窗口标题栏新增「小窗」按钮：点击后固定置顶并缩到屏幕右上角（420 × 300），再点「还原」恢复原来的大小与位置；小窗模式会收起键盘提示与置顶按钮，给预览内容留出空间
- 空格预览支持**浏览历史**：预览窗口打开期间，在资源管理器里换了选中文件后再按空格会直接预览新文件，不再先关窗再按一次；标题栏新增左右箭头，`←` / `→` 以及原来的 `↑` / `↓` 可在本次打开期间预览过的文件之间前后切换。历史按文件路径去重，重复预览同一文件会移到队列末尾，`PositionText` 显示当前位置
- 全局启动器支持**框选多选、拖拽排序与启动器文件夹**：
  - 在空白处按住拖拽绘制选框，实时选中与选框相交的图标；`Ctrl` + 单击切换单个图标的选中态，`Ctrl+A` 全选，`Delete` 批量移除
  - 拖动任意图标调整位置，顺序与分组保存到 `data.json` 的 `LauncherLayout`；多选后可整体拖动、批量移除或右键「合并为文件夹」
  - 把一个图标拖到另一个图标的图标中心即合并为启动器文件夹；点击文件夹磁贴会在居中的浮层卡片里展开内容（类似平板文件夹，不占满启动器，底层页签仍可见），没有单独的返回按钮，点击卡片外空白或按 `Esc` 收起；支持重命名、解散、把条目移出文件夹，再把新图标拖到文件夹磁贴上即可加入
  - 系统工具页与搜索结果只读，不参与排序、框选与分组；布局在打开时与真实条目自动对账，删除快捷方式后空文件夹会清理、单条目文件夹会降级
- 全局启动器支持 **`Ctrl` + 鼠标滚轮切换页签**：上滚上一页、下滚下一页，与 `←` / `→` 行为一致，搜索期间不切换
- 「快捷应用」顶部新增「文件浏览器」入口：与剪贴板、翻译、开发环境并排的第四个正方形磁贴，点击后收起快捷应用并打开文件浏览器窗口（目录导航 + 内嵌预览）
- 「快捷应用」顶部新增「开发环境」入口：与剪贴板、翻译并排的第三个正方形磁贴，点击后收起快捷应用并显示主窗口
- 「快捷应用」结果列表改为**单击启动**：点中任意一项立即打开应用、文件夹、网站或文件，不再需要双击；右键菜单、方向键与回车操作保持不变
- 「快捷应用」的搜索范围扩展到全局启动器中的**本地应用、文件夹与网站**：关键字同时匹配名称、路径 / 网址与类型名（如「网站」），结果按类别标注，回车直接启动应用、在资源管理器中打开文件夹或在浏览器打开网站；快捷方式条目不会再出现在「固定到快捷应用」菜单里
- 新增「服务监控」页（左侧导航，位于「进程监控」下方），内置 Windows 服务面板以替代系统的 `services.msc`：
  - 列出服务的显示名、服务名、状态、启动类型、登录身份与描述，支持按显示名 / 服务名 / 描述 / 路径搜索，按状态筛选并按显示名 / 服务名 / 状态排序
  - 支持启动、停止、重启、暂停 / 继续，以及把启动类型改为自动、自动（延迟启动）、手动或禁用；停止前会检查仍在运行的依赖服务
  - 选中服务后可查看可执行文件、登录身份、服务类型、依赖服务与被依赖服务，并可直接在资源管理器中定位可执行文件
  - 读取服务列表不需要管理员权限，启停与修改启动类型需要；状态栏显示当前权限，工具条提供「管理员重启」
  - 「系统工具 → 服务」入口改为打开内置页面，不再拉起 `services.msc`；新增「服务监控」文档页与官网入口
- 合并 SigXor 独立应用到本仓库，作为「文件预览与语音」模块并入燧岚开发盒子：
  - **文件快速预览**：在资源管理器中选中文件按空格即可弹出 Quick Look 式浮窗，支持 13 种内容类型——纯文本、代码、Markdown、网页、CSV、SVG、字体、图片、音视频、PDF、Office（Word / Excel / PowerPoint）、压缩包、十六进制二进制；Hex 编辑器支持查找 / 跳转 / 修改 / 保存
  - **快捷应用**：按 `Alt+Space` 全局唤起，可搜索并启动应用、打开文件、执行命令，支持固定常用项
  - **文件浏览器**：目录导航 + 内嵌预览面板，支持前后翻页、拖入文件直接预览
  - **语音输入**：基于 sherpa-onnx / SenseVoice 的本地语音转写，浮窗显示状态，结果可直接注入光标处
  - **区域截图与 OCR**：拖动选区截图，支持整页 OCR 取字、像素取色、复制与保存；OCR 模型本地管理
  - **命令行直接预览**：`DevEnv.exe <文件路径>` 打开预览浮窗；二次启动时经命名管道把路径转给主实例
- 新增 SigXor 模块自检探针 `--sigxor-check`（含 `--sigxor-smoke` / `--sigxor-preview-smoke` / `--sigxor-theme-smoke`）：自动验证 34 个窗口 / 视图能否加载、13 类预览内容能否渲染、深浅主题切换是否生效。WPF 的页面级 XAML 异常是静默的，靠肉眼点菜单不可靠
- 「工具」菜单与系统托盘新增文件预览相关入口：文件快速预览、快捷应用、文件浏览器、区域截图、语音与截屏设置、关于文件预览模块
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

- **基础包瘦身与可选组件按需下载**：`DevEnv.exe` 只保留 Magick.NET 等核心依赖，`DocumentFormat.OpenXml`（Office 结构化预览）、`RapidOcrNet` + SkiaSharp（截图 OCR）、`org.k2fsa.sherpa.onnx`（语音输入）与共用的 `onnxruntime` 改为 3 个可按需安装的组件（Office / OCR / 语音），安装到 `%LOCALAPPDATA%\DevEnv\Components`。基础单文件 exe 从约 89 MB 降到约 50 MB；组件从 GitHub Release 的 `components.json` 读取，下载后校验 SHA256。缺组件时 Office 预览自动降级到网页引擎，OCR / 语音给出明确安装提示。设置 →「服务与模型」新增组件下载、删除与打开目录
- **分发改便携 zip + 自研更新**：发布物改为 `DevEnv-<ver>-win-x64.zip`（exe + Resources + 许可证），解压即用；应用通过 `latest.json` 检查更新，下载新版 zip、校验 SHA256，由内嵌 `--updater` 第二进程替换并看门狗回滚。移除 Velopack 安装器、Setup.exe、nupkg 与 delta 升级。旧 Velopack 安装用户需要一次性下载便携 zip 迁移
- **主窗口拆分为开发窗口与监控窗口**：开发环境 / 熔岩环境留在 **开发窗口**，网络监控 / 进程监控 / 服务监控移入新的 **监控窗口**；两个窗口共用同一套界面，只替换标题与左侧导航（`ViewFactory.DevelopmentNavItems` / `MonitorNavItems`）。开发窗口启动时创建并隐藏驻留托盘，监控窗口第一次从启动器内置工具页进入时才创建；托盘「显示主窗口」、`Ctrl+Alt+D` 与二次启动仍唤回开发窗口
- **全局启动器换页动态效果与位置修正**：`Ctrl` + 滚轮、`←` / `→` 或点页签换页时，新页内容按切换方向滑入并淡入（190ms / 150ms，EaseOut），同时把网格滚回顶部；底行固定高度，修复切换到系统工具 / 内置工具页时「＋」按钮隐藏导致底行变矮、页签上下跳的问题
- **全局启动器动画强化**：换页后卡片按索引错峰入场（每张晚 9ms，最多 24 张，向上 10 DIP 淡入上浮）；磁贴悬停放大到 1.05 并提到上层、移出平滑复位；文件夹浮层以 90% 到 100% 的 `BackEase` 回弹弹出、暗底同时淡入；页签选中时轻弹一下；打开 / 收起时整窗在 1.015 与 1 之间轻微缩放，和透明度淡入淡出叠加
- **全局启动器改为居中悬浮面板**：不再铺满整个屏幕，改为占主屏工作区宽高各 78% 并居中（约六成面积），分层窗口每次合成的像素明显减少，打开与动画更轻快，也不会盖住任务栏；点击面板外的桌面会因失焦自动收起。`ApplyScreenBounds` 的几何比较同步改为与目标位置比较（原实现假设 `Left` / `Top` 恒为 0）
- **全局启动器改为 iOS 风格磨砂玻璃**：打开前抓取窗口背后的桌面并缩到 1/10 再拉伸铺满，得到真实的磨砂虚化底图，上面叠白色半透明玻璃色与顶部高光；根元素按 22 DIP 圆角裁剪并加一圈白色高光边。文字、搜索框、磁贴、页签、文件夹浮层与框选强调色统一换成浅色玻璃配色（深色文字与 iOS 蓝），不再使用原来的深色渐变。截不到图（锁屏 / 远程会话断开 / 受保护窗口）时退化为纯玻璃色
- **全局启动器与开始菜单互斥**：打开启动器前先检测开始菜单（前台或可见的 `StartMenuExperienceHost` / `SearchHost` 窗口），开着就先发 Esc 收起，并延迟约 120ms 再显示启动器，避免两个置顶窗口叠在一起；启动器显示期间安装轻量 Win 键钩子，用户按 Win 要打开开始菜单时先把启动器淡出收起，隐藏后立即卸载钩子。Win 键只监听不拦截，开始菜单照常弹出
- **全局启动器改用左 Alt 唤起**：单独按一下左 Alt 键打开 / 收起全局启动器，Win 键不再被拦截，系统开始菜单恢复原样。新钩子**不吞任何按键**，`Alt+Tab` / `Alt+F4` / `Alt+空格` 等组合键不受影响，前台程序也不会因为缺少抬起而把 Alt 误判成一直按着；右 Alt（AltGr、语音键）永不触发。配置项 `Config.WinKeyLauncher` 更名为 `Config.AltKeyLauncher`（旧字段不再读取，缺省仍为开启），设置页勾选项改为「单独按一下左 Alt 键打开全局启动器」；自检探针由 `--winkey-check` 改为 `--altkey-check`
- **主窗口「工具」菜单整体移除，工具并入全局启动器**：全局启动器在「系统工具」之后新增「内置工具」页签，包含 JSON 格式化器、Hosts 文件编辑器、图像格式转换器、文件哈希计算器、文件解锁与粉碎、启动项管理、右键菜单管理，点击直接打开对应的独立工具窗口；该页签与系统工具页一样只读，不参与排序、分组与添加。主窗口顶部不再保留任何菜单，`DEVENV_START_PAGE=builtin` 可直接打开该页签，`--shortcuts-check` 同步覆盖五个页签
- **主窗口「工具」菜单移除 SigXor 相关入口**：文件快速预览、快捷应用、文件浏览器、区域截图、语音与截屏设置五项连同分隔线一并去掉，菜单只保留 JSON / Hosts / 图像 / 哈希 / 文件解锁等小工具与启动项、右键菜单管理；这些能力统一从托盘右键菜单、快捷应用顶部入口或设置进入
- **主窗口顶部菜单精简为只有「工具」**：移除「设置」与「帮助」两个菜单，设置统一从托盘右键菜单进入，关于与帮助文档并入设置对话框
- **托盘右键菜单新增「设置…」并移除「关于…」**：设置对话框成为唯一的设置与关于入口；原来散落在主菜单和模块关于窗里的关于、检查更新、帮助文档入口全部收拢到这里
- **设置新增第五个页签「关于与帮助」**：展示产品名、版本、产品简介与版权，官网 / 开源地址 / 使用文档链接，飞书交流群二维码，以及从「常规」页迁移过来的检查更新卡片；检查更新逻辑从 `SettingsViewModel` 拆到新的 `AboutViewModel`，`SettingsView` 只保留常规选项
- **删除两个独立关于窗口**（宿主 `Windows/AboutWindow` 与模块 `SigXor/AboutWindow`）及其入口：主菜单「关于燧岚开发盒子」、托盘「关于…」、模块 `SigXorHost.OpenAbout` 与 `HostCallbacks.OnOpenSigXorAbout` 一并移除，避免同一份信息维护两套
- **全局启动器改为单击图标直接启动**：单击即运行并收起，不再受「双击启动快捷方式」影响；多选状态下第一次单击仍只收窄选择，`Ctrl` + 单击与拖拽排序不变。设置「常规」里已无实际作用的「双击启动快捷方式」开关与 `data.json` 的 `DoubleClickToLaunch` 字段一并移除
- **全局启动器点击空白处不再关闭窗口**：空白处改为框选起点，单击只清除选择；关闭仍用 `Esc`、托盘 / Win 键入口或失去焦点。单击图标仍照常启动并收起
- **设置新增「快捷键」页签，所有程序绑定的快捷键集中可改**：显示主窗口（`Ctrl+Alt+D`）、快捷应用（`Alt+Space`）、Win 键启动器开关、语音输入、区域截屏、文件快速预览打开 / 关闭键统一到「设置 → 快捷键」。全局组合键点击后直接录制，要求至少含 Ctrl / Alt / Win 之一并拒绝系统保留组合；录制期间自动挂起 `RegisterHotKey`、Win 键钩子与 SigXor 预览 / 语音 / 截屏钩子，避免按到旧快捷键触发原动作。`AppConfig` 新增 `QuickAppHotkey`，SigXor 设置新增 `Preview.OpenShortcut` / `Preview.CloseShortcut`；常规页保存改为 `SaveGeneral`，快捷键页使用 `SaveHotkeys`，避免两个页签互相覆盖
- **「系统工具」从主窗口移入全局启动器**：启动器在「网站」之后新增第四个「系统工具」横向页签，按系统管理 / 硬件与设备 / 网络与安全 / 性能与监控分类展示全部 Windows 工具，单击启动、右键「运行」，搜索框同时匹配工具名称、分类与说明；主窗口左侧不再保留「系统工具」导航页，`DEVENV_START_PAGE=tools` 改为直达该页签
- **主窗口顶部菜单精简**：移除「文件」（含退出）、「熔岩翻译」「熔岩剪贴板」「全局启动器」四项，只保留「工具」「设置」「帮助」。翻译 / 剪贴板 / 全局启动器 / 退出仍可从托盘右键菜单或快捷应用顶部入口进入
- **快捷应用入口位置调整**：剪贴板与翻译两个正方形磁贴从搜索框上方移到搜索框下方，搜索框保持在窗口最顶部，输入后视线自然落到结果列表
- **翻译与剪贴板不再占用全局快捷键**：移除 `Alt+Space`（熔岩翻译）与 `Ctrl+Space`（熔岩剪贴板）的 `RegisterHotKey` 注册，`Ctrl+Space` 归还给系统、输入法与其它软件。快捷应用窗口顶部新增「剪贴板」「翻译」两个正方形入口：剪贴板直接打开剪贴板窗口，翻译打开翻译窗并沿用原行为自动填充剪贴板文本；相关设置、关于、入口提示与文档同步更新
- **快捷应用改用 `Alt+Space` 全局唤起**：`GlobalHotkeys` 新增 `IdLauncher` 热键并绑定 `SigXorHost.ShowLauncher`，注册失败时托盘气泡提示；同时删除 `ShellPreviewService` 中「任务栏聚焦时按空格打开快捷应用」的分支，空格恢复为系统与其它程序的正常按键，任务栏场景不再触发启动器。托盘单击、主菜单与工具目录入口保持不变
- **两个启动器更名**：SigXor 的搜索框式启动器「全局启动器」更名为「**快捷应用**」（主菜单「工具 → 快捷应用」、托盘单击、系统工具目录同步改名）；本仓库的 Launchpad 式「快捷方式启动台」更名为「**全局启动器**」（主菜单「全局启动器」、托盘右键菜单「全局启动器…」、设置里的「按 Win 键打开全局启动器」同步改名）。类名 `ShortcutsWindow` / `ShortcutWindowHost` 与 SigXor 的 `LauncherWindow` 保持不变
- **启动时不显示任何窗口**：主窗建好后保持隐藏，程序只驻留托盘；需要主窗时按 `Ctrl+Alt+D` 或从托盘右键菜单「显示主窗口」唤出。翻译窗 / 剪贴板窗若在主窗隐藏期间创建则不挂 Owner（owner 隐藏时 owned 窗口不会显示），主窗显示后自动补挂
- **托盘入口调整**：单击托盘图标改为打开 **快捷应用**（搜索并启动应用 / 文件 / 命令，原来是唤回主窗口）；**全局启动器**（原「快捷方式启动台」）改从右键菜单「全局启动器…」或 Win 键进入；**主窗口**只从右键菜单「显示主窗口」进入，单实例二次启动与 `Ctrl+Alt+D` 仍直接唤回主窗口。启动台因失焦自动收起后 500ms 内的再次唤出按「关闭」处理，不会收起后又立刻弹出
- 快捷方式改为 **Launchpad 式全屏启动器**（现名「全局启动器」）：「本地应用 / 文件夹 / 网站」从主窗左侧导航移出，主菜单「全局启动器」打开铺满屏幕的深色启动器——顶部搜索框（跨三类合并匹配，显示结果条数）、中部大图标网格（固定列宽、逐行居中）、底部横向页签；单击图标即启动，`Esc` 或点击空白处关闭，`←` / `→` 切页，`F5` 刷新，支持把 `.exe` / 文件夹直接拖入添加，右键图标可运行、定位、改名、刷新图标与移除。主窗左侧导航只保留开发环境、熔岩环境、网络监控、进程监控与系统工具，默认页改为「开发环境」；`DEVENV_START_PAGE=apps|folders|webs` 与「跳到页面」类入口统一改为打开启动器的对应页签。原先的卡片视图 `AppsView` / `FoldersView` / `WebsView` 及其 ViewModel 已删除，统一由启动器承载
- 应用图标改为优先提取 **256px 大图标**（`PrivateExtractIcons`，取不到时逐级回退到 48px / 系统默认 32px）；旧版按 32px 缓存的图标在首次打开启动器时自动重新提取，`刷新图标` 现在会真正覆盖缓存而不是被 `SaveIcon` 的「已存在即跳过」挡住
- 新增启动器自检探针 `--shortcuts-check [报告路径]`：加载窗口、遍历三个页签、跑一次搜索并收集 WPF 数据绑定错误，结果写报告文件；用法与 SigXor 自检探针一致
- **单独按一下 Win 键打开全局启动器**，再按一次收起：新增 `WH_KEYBOARD_LL` 键盘钩子（`WinKeyLauncherHook`）与纯逻辑手势判定 `WinKeyGestureTracker`（10 个单测覆盖）；只吞 Win 键抬起、不吞按下，因此 `Win+E` / `Win+R` / `Win+D` / `Win+Tab` 等组合键完全不受影响，按住 Win 的自动重复、左右 Win 同时按、组合键后再单按都按预期处理；吞掉抬起后会补发「假按键 + Win 抬起」让系统按键状态复位（否则系统一直认为 Win 键按着，之后所有按键都会变成 Win+组合键），并用假按键打断开始菜单的单击判定；注入按键（宏键盘 / 自动化工具）不触发；模态对话框打开期间不抢 Win 键。启动台不挂 Owner，Win 键只唤出启动台本身，不会连带显示主窗口。开关为 `data.json` 的 `Config.WinKeyLauncher`（默认开启），可在「设置 → 常规」取消勾选恢复系统开始菜单，需要开始菜单时也可按 `Ctrl+Esc`
- 新增 Win 键拦截自检探针 `--winkey-check [报告路径]`：真实安装钩子并用 `SendInput` 模拟按键，验证单按触发、组合键放行、卸载后失效
- SigXor 的「文件预览与语音设置」独立模态窗口**已删除**，内容并入燧岚开发盒子的设置对话框，设置窗口改为**平铺四页签**（无二级页签）：「常规」= 主程序设置 + 文件预览的常规选项（字体 / 备份），「快捷键」，「语音与截屏」，「服务与模型」；主菜单「设置」相应拆成「常规 / 快捷键 / 语音与截屏 / 服务与模型」四项。原先散落在 SigXor 设置里的开机自启、托盘图标等全局选项去掉重复入口，统一在「常规」页签管理。设置窗口改为全局单实例（`SettingsDialogHost`），多个入口不再各弹一个框
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

- 修复开发构建（`make run` 的 Debug 输出）里右 Alt 语音输入无反应：可选组件检测只认输出目录根部的关键文件，而 `sherpa-onnx-c-api.dll` / `onnxruntime.dll` 等原生依赖由 NuGet 放在 `runtimes\<rid>\native` 下，`ComponentStore.IsInstalled` 因此误判为「语音引擎组件未安装」，语音服务在开机自启时直接跳过，按键时又因通知开关关闭而没有提示。现组件关键文件同时到 `runtimes\win-<arch>\native` 下查找，开发构建无需先下载组件即可启动语音与 OCR
- 修复发布出的单文件 `DevEnv.exe` 高达 255 MB：`.NET 8+` 的 SDK 在 `PublishSingleFile=true` 时会推断 `SelfContained=true`，而 `--self-contained false` 的空格写法不会被转发为显式 `false`，于是本该框架依赖的发布把整个 .NET 10 Desktop Runtime（约 160 MB，含 WPF / WinForms / BCL 与本地化资源）一起打进了 exe。`scripts/publish.ps1`、`release.yml` 与发布文档统一改用 `--no-self-contained`，体积降到约 89 MB；目标机需安装 .NET 10 Desktop Runtime（便携版不携带运行时）
- 修复资源管理器与桌面里 `Alt` + 空格呼不出快捷应用：空格预览的低级键盘钩子只按虚拟键匹配预览键，没有排除修饰键，`Alt` / `Ctrl` / `Win` 组合会被 `WH_KEYBOARD_LL` 吞掉，`RegisterHotKey` 收不到 `Alt` + 空格。现在预览与关闭键在检测到 `Alt` / `Ctrl` / `Win` 按下时直接放行，组合键交还给系统与全局热键（`Alt` + `Esc` 切换窗口等也一并恢复）
- 修复文本预览在浅色主题下仍是深色背景：`TextView` 的编辑器底色、前景与行号颜色改为跟随主题资源（`SurfaceBrush` / `TextPrimaryBrush` / `TextMutedBrush`），不再写死深色。语法高亮配色同步改为按主题切换，深色用 VS Code Dark、浅色用 VS Code Light，`SyntaxHighlightingService` 订阅 `ThemeManager.ThemeChanged` 重新注册定义，已打开的编辑器在主题切换后立即刷新。`sigxor-theme-smoke` 探针新增编辑器底色随主题切换的断言
- 修复文件快速预览把 YAML 等文本文件当成二进制：扩展名无法确定或被标为二进制时，改为读取文件头按内容判断文本 / 二进制。文本嗅探支持 UTF-8、带 BOM 的 UTF-16、无 BOM 的 UTF-16 以及中文环境常见的 GBK / GB18030，并用控制字符比例排除压缩包、图片、可执行文件等真实二进制；无扩展名的 YAML / 模板文件、扩展名写成 `.dat` / `.bin` 的文本都会进入文本预览，真实二进制仍然使用十六进制视图。`.yaml` / `.yml` 继续按代码类型预览
- 修复文件浏览器点标题栏关闭按钮后关不掉、界面像卡住：`OnClosing` 先取消本次关闭再排队异步收尾，收尾完成调用 `Close()` 前又把 `_hideRequested` 置回 `false`，第二次 `Closing` 仍按「首次关闭」处理，再次取消并重新排队，形成无限循环，窗口永远关不上。现在收尾完成后置 `_exitRequested = true` 再 `Close()`，放行真正的关闭
- 修复机器缺少 VC++ 运行库时程序启动后无声退出：语音服务开机自启会构造 `OfflineRecognizer`，其原生库 `sherpa-onnx-c-api.dll` 依赖的 `onnxruntime.dll` 需要 `MSVCP140` / `VCRUNTIME140`，未安装「Microsoft Visual C++ 2015-2022 运行库（x64）」时加载失败；而 sherpa-onnx 的托管封装在构造函数抛异常后对象仍可被终结，finalizer 线程上的 `DllNotFoundException` 会直接终止进程（退出码 `0xE0434352`，`crash.log` 里只有一条 `OfflineRecognizer.Finalize` 记录）。现在构造前先用 `NativeLibrary.TryLoad` 探测原生库，加载失败只给出明确提示并跳过构造，语音识别不可用但程序照常运行
- 全局启动器改为**启动时预热**：App 启动后在应用空闲时把窗口在屏幕外、透明、不抢焦点的状态下显示一次再隐藏，完成 HWND 创建、模板加载、布局与首帧渲染；之后 Win 键 / 托盘菜单唤起只是把已有窗口摆出来，不再临时创建，避免首次打开先闪空白再闪内容。`ShortcutWindowHost.Prewarm()` 失败时静默忽略，只是首帧慢一点
- 修复全局启动器显示时先闪一下：新窗口的 `Left` / `Top` 初始为 `NaN`，而 `Math.Abs(NaN) > 0.5` 恒为 false，几何修正被跳过，`Show()` 时由 WPF 临时挑位置（屏幕中部），窗口先出现在错误位置再跳到全屏；现改为几何比较同时判 `NaN`，并在预热阶段就落在最终位置。另外分层窗口会缓存最后一次合成结果：原先收起时先 `Hide()` 再把内容设成全透明，窗口已不可见、不再渲染，缓存里留的仍是不透明画面，**下次 `Show` 的头一帧就是它**——这就是「第一次打开正常、之后每次打开都闪一下」的原因。现在收起改为**先淡出到全透明、窗口仍可见时完成这一帧渲染，再 `Hide()`**，缓存里存的是透明画面；淡出期间再次唤起会取消收起直接淡回
- 修复全局启动器进入时闪两下：淡入从 `Window.Opacity` 改到根元素的 `Opacity`（分层窗口首次显示会先按不透明画一帧再被拉回 0），激活改为先比较系统前台窗口、只在确实不是自己时强制前置一次，并删掉宿主里重复的 `Show()` / `Activate()`；新增 400ms 激活宽限，避免前台权限交接期间收到 Deactivated 就自行收起
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

- 移除 Velopack 依赖与相关基础设施：两个 csproj 的 `Velopack` 包、`VelopackApp` 启动钩子、`GithubSource` 更新源、vpk 打包 / 上传步骤与 `make installer` 目标；更新检查与回滚改由 `DevEnv.Core/Updates` 自研实现
- 移除文档站部署工作流 `.github/workflows/docs.yml`：文档站不再由 GitHub Actions 发布，改由本机 `make deploy-docs`（或 `website/` 下 `npm run deploy`）构建并部署到 Cloudflare Pages
- 移除本机 HTTP API：`:35174` 端口、会话 token 鉴权、`wwwroot` 静态资源托管（图片不再经 HTTP，直接绑本地文件）
- 移除主壳 WebView2：界面不再嵌入 Chromium，只有熔岩翻译窗保留 WebView2（DOM 填充需要真实浏览器）
- 移除 Avalonia 主程序 `DevEnv/` 与共享控件库 `Lava.UI/`
- 内嵌模块迁至 `libs/`，并删除 Views / UI / `*.axaml` 等停用界面源文件；只保留供服务层 / Host 使用的 Services / Models
- 删除未引用的独立项目 `WinBootSelfStarting/`（启动项已由 `DevEnv.Core` 承接）与对应过时文档
- 删除独立工程 `Clipboard` / `NetworkWatch` / `ProcessWatch` / `Translator` / `LavaEnv` 与独立应用 `MenuManager`（含其独立 sln、About 对话框）；功能已并入 `DevEnv.Core` 与 `DevEnv.Host`，右键菜单配置路径仍为 `文档\FNSoftware\MenuManager\menu_configs.json`

### Fixed
- 全局启动器补上窗口外点击监听（`OutsideClickDismissHook`，`WH_MOUSE_LL`）：点击任务栏空白处、窗口尚未真正拿到前台权限等不会触发 `Deactivated` 的场景也会收起；钩子只在启动器可见期间安装，收起或退出时卸载

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
