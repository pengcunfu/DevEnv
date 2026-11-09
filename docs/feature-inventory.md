# 功能实现清单（WPF 迁移对账）

对账基准：**迁移前的 `git HEAD` 版本**（WinForms + WebView2 + React + 本机 Minimal API，91 个 HTTP 接口 / 17 个 React 页面）对 **当前工作区版本**（原生 WPF）。

- 生成时间：迁移完成后
- 校验手段：编译 0 错误 0 警告 · 命令绑定静态审计 0 断链 · 右键菜单进程内探针 52/52 接通 · 发布版实跑

---

## 一、对账结论

本轮对账查出并修复了 **7 类真实缺陷**。它们都是 WPF 特有的**静默失效**：编译不报错、界面照常渲染，只有点了才知道没反应。

| # | 缺陷 | 影响面 | 修复 |
|---|------|--------|------|
| 1 | **卡片单击/双击启动直接抛异常**：`Button.Click` 抛的是 `RoutedEventArgs`，代码里强转 `MouseButtonEventArgs` 取 `ClickCount` → `InvalidCastException` | **本地应用 / 文件夹 / 网站三页的卡片启动交互全挂**（这是三页最核心的操作） | 改挂 `MouseLeftButtonUp`（它才是 `MouseButtonEventArgs`），处理器更名 `OnCardMouseUp` |
| 2 | **卡片右键菜单命令全部失效**：`ContextMenu` 显示时 WPF 会把 `DataContext` 覆盖成 `PlacementTarget` 的（即条目对象），菜单里的 `Command="{Binding RunCommand}"` 解析不到 | 本地应用 5 项 + 文件夹 3 项 + 网站 4 项 + 空状态 2 项 = **14 个菜单命令点了没反应** | 改为 `Tag` + `Click` 显式分派（`OnCardMenuClick`），不依赖 `DataContext` 语义 |
| 3 | `ReloadCommand` 绑定无效：基类方法名带 `Command` 后缀，生成器拼成了 `ReloadCommandCommand` | 三个列表页空白处右键「刷新」失效 | 方法改名 `LoadItems`，绑定同步更新 |
| 4 | 熔岩「数据目录」页缺 **打开 config.json**、**刷新** 两个按钮 | React 版有，迁移时漏了 | 补回并接线 |
| 5 | **菜单栏带子菜单的项点开是空的**：自定义 `MenuItem` 模板只渲染了标题的 `ContentPresenter`，没有 Popup + ItemsPresenter，子项永远不出现 | **文件 / 工具 / 帮助 三个菜单及其全部子项（退出、5 个工具页、官方文档、关于）** | 模板补 `Popup` + `ItemsPresenter`，`IsOpen` 绑 `IsSubmenuOpen` |
| 6 | **文件哈希计算器页面加载即抛异常**：`<Style TargetType="RadioButton" BasedOn="{StaticResource {x:Type CheckBox}}"/>` 非法——WPF 只允许 BasedOn 自己或**自己的基类**，CheckBox 与 RadioButton 互不为基类（都派生自 ToggleButton），样式 Seal 时抛 `InvalidOperationException` | 从「工具」菜单进哈希页**整页加载失败、点了没反应** | 公共部分抽到 `ToggleButton` 基样式，两个控件各自模板 |
| 7 | **三页卡片图标不显示**：data.json 里的 `IconPath` 存的是目录迁移**之前**的旧路径（旧目录已搬空），同名文件其实就在当前图标目录里 | 本地应用 / 文件夹 / 网站 共 10 条图标全部失效 | 新增 `IconHelper.ResolveIconPath()`：旧路径不存在时按文件名回退到当前图标目录 |

另外补记一条此前发现的隐患：`Directory.Build.props` 的 obj 重定向会污染 WPF 的 `_wpftmp` 临时工程（报 `NETSDK1004`），当时已加条件跳过；该根级重定向现已整体移除，构建产物回到各项目自己的 `bin/`、`obj/`，这条隐患随之消失。

### 审计手段（可复跑）

| 手段 | 作用 | 结果 |
|------|------|------|
| 命令绑定静态审计（以**源生成器真实产物**为基准，不用猜命名规则） | 找出「绑了不存在的命令」 | 65 处绑定，0 断链 |
| 进程内菜单探针 | 逐个打开右键菜单，断言每个菜单项有 `Command` 或 `Tag` 分派 | 52 项全部接通 |
| 进程内交互探针 | 反射驱动卡片处理器，验证「视图 → 视图模型 → 服务层 → 系统动作」 | 视图模型命令成功拉起 `terminal64.exe` |
| 真实鼠标点击 + `crash.log` | 抓运行时异常 | 修复后单击/双击/右键均无异常 |
| 菜单栏实机验证 | 点开一级菜单，看子项是否出现并能执行 | 文件→退出（进程归零）、帮助→关于（弹出关于窗）；顶栏项宽度 70~118px |
| 哈希页探针 | 用真实文件跑一次计算并比对真实 SHA256 | 按钮绑定有效、结果与 SHA256 完全一致 |
| 菜单栏实机验证 | 点开一级菜单，看子项是否出现并能执行 | 文件→退出（进程归零）、帮助→关于（弹出关于窗）均通过 |

---

## 二、接口对账：91 个 HTTP 接口 → 服务层 → 界面入口

| 分组 | 接口数 | 现在的实现 | 界面入口 | 状态 |
|------|-------:|-----------|---------|------|
| 主壳 / 配置 / 对话框 | 12 | `SettingsService` + `HostCallbacks` | 设置页 / 各页按钮 / 菜单 | ✅ |
| 快捷方式（应用·文件夹·网站） | 16 | `ShortcutService` + `StartMenuService` | 启动器（Launchpad）本地应用 / 文件夹 / 网站三页 + 只读「开始菜单」页 | ✅ |
| 系统工具 | 2 | `ShortcutService.GetSystemTools / RunSystemTool` | 启动器（Launchpad）搜索框右侧「系统工具」文件夹 | ✅ |
| 进程监控 | 3 | `ProcessService` | 进程监控页 | ✅ |
| 网络监控 | 8 | `NetworkService` | 网络监控页（流量/端口/测速） | ✅ |
| 熔岩 - 进程与目录 | 9 | `LavaService` | 熔岩环境页 | ✅ |
| 熔岩 - 下载与镜像 | 12 | `LavaService` | 熔岩环境页 | ✅ |
| 熔岩剪贴板 | 7 | `ClipboardService` | 剪贴板独立窗 | ✅ |
| 开发环境 | 4 | `DevEnvService` | 开发环境页 | ✅ |
| 熔岩翻译 | 4 | `TranslatorService` | 翻译入口页 / 设置页 | ✅ |
| 启动项 | 3 | `StartupService` | 启动项管理页 | ✅ |
| 实用工具 | 6 | `UtilityService` | JSON / Hosts / 哈希 / 图像 四页 | ✅ |
| 自更新 | 4 | `UpdateService` | 设置页 | ✅ |
| 媒体（图片） | 1 | 改为 `ClipboardEntryView.ImageFullPath` 直接给本地路径 | 剪贴板窗 | ✅ 取代 |
| **合计** | **91** | 12 个服务类 | 16 个页面 | ✅ |

**逐项去向**（接口 → 服务方法 → 界面）：

- `GET/PUT /api/config` → `SettingsService.GetConfig / SaveGeneral`（常规设置，派发开机自启、托盘副作用）；快捷键由 `SaveHotkeys` 单独写入并派发 `OnGlobalHotkeysChanged`（「设置 → 快捷键」页）
- `GET /api/health` → 程序集版本 → 状态栏 + 关于窗（不再需要 HTTP 回环）
- `/api/session` → **取消**（已无 HTTP，token 无意义）
- `/api/nav` → `ViewFactory.NavItems`（前端原本就硬编码，等价迁移）
- `POST /api/shell/*`（7 个）→ `HostCallbacks` 委托 → WPF 对话框 / 独立窗 / 页面跳转
- `native-apps / folders / webs`（16 个）→ `ShortcutService` 同名方法 → 三个卡片页
- `GET /api/system-tools` + `run` → `GetSystemTools / RunSystemTool` → 全局启动器搜索框右侧「系统工具」文件夹（19 个磁贴）
- `/api/processes` 三件套 → `ProcessService.GetSnapshot / Kill / OpenFolder` → 进程监控页
- `/api/network/*` 八个 → `NetworkService`（含测速状态机原样搬运）→ 网络监控页三页签
- `/api/lava/*` 二十一个 → `LavaService` → 熔岩环境页七页签
- `/api/clipboard/*` 七个 → `ClipboardService` → 剪贴板独立窗（纯 WPF，图片直读本地文件）
- `/api/devenv/*` 四个 → `DevEnvService` → 开发环境页
- `/api/translator/*` 四个 → `TranslatorService` → 翻译入口页 + 设置页
- `/api/startup/*` 三个 → `StartupService` → 启动项管理页
- `/api/utils/*` 六个 → `UtilityService` → 四个工具页
- `/api/update/*` 四个 → `UpdateService` → 设置页
- `GET /api/media/file` → **不再需要**：剪贴板图片直接给绝对路径

---

## 三、页面清单：17 个 React 页面 → 16 个 WPF 视图

| React 页面 | WPF 视图 | 导航 id | 状态 |
|-----------|-----------|---------|------|
| `Layout`（菜单/侧栏/状态栏） | `MainWindow.xaml` | — | ✅ |
| `AppMenuBar`（含关于窗） | `Menu` + `AboutWindow` | — | ✅ |
| `ContextMenu`（卡片右键） | 三个视图的 `ContextMenu`（Tag 分派） | — | ✅ 本轮修复 |
| `AppsPage` / `FoldersPage` / `WebsPage` / `StartMenuPage` | `ShortcutsWindow`（Launchpad 启动器） | `apps` / `folders` / `webs` / `startmenu` | ✅ |
| `DevEnvPage` | `DevEnvView` | `devenv` | ✅ |
| `LavaPage`（7 个 Tab） | `LavaView`（7 个页签） | `lava` | ✅ |
| `NetworkPage`（3 个 Tab） | `NetworkView`（3 个页签） | `network` | ✅ |
| `ProcessesPage` | `ProcessesView` | `processes` | ✅ |
| 系统工具（启动器文件夹） | `ShortcutService.GetSystemTools` → 搜索框右侧「系统工具」浮层 | `tools` | ✅ |
| `JsonToolPage` | `JsonToolView` | `tools/json` | ✅ |
| `HostsToolPage` | `HostsToolView` | `tools/hosts` | ✅ |
| `HashToolPage` | `HashToolView` | `tools/hash` | ✅ |
| `ImageToolPage` | `ImageToolView` | `tools/image` | ✅ |
| `StartupToolPage` | `StartupToolView` | `tools/startup` | ✅ |
| `TranslatorPage` | `TranslatorEntryView` + `TranslatorWindow` | `translator` | ✅ |
| `ClipboardPage` | `ClipboardWindow`（独立窗）+ `ClipboardEntryView` | `clipboard` | ✅ |
| `SettingsPage` | `SettingsView` | `settings` | ✅ |

---

## 四、交互控件清点（当前界面实际可点的东西）

| 视图 | 按钮 | 菜单项 | 表格列 | 输入控件 |
|------|-----:|-------:|-------:|--------:|
| ShortcutsWindow（启动器） | 1 | 8 | 0 | 1 |
| DevEnvView | 3 | 0 | 5 | 0 |
| LavaView | 28 | 0 | 27 | 4 |
| NetworkView | 7 | 0 | 17 | 2 |
| ProcessesView | 5 | 0 | 6 | 2 |
| JsonToolView | 2 | 0 | 0 | 1 |
| HostsToolView | 3 | 0 | 0 | 1 |
| HashToolView | 2 | 0 | 0 | 3 |
| ImageToolView | 3 | 0 | 4 | 3 |
| StartupToolView | 3 | 0 | 5 | 2 |
| SettingsView | 6 | 0 | 0 | 3 |
| TranslatorEntryView | 3 | 0 | 0 | 0 |
| ClipboardEntryView | 1 | 0 | 0 | 0 |
| **视图合计** | **70** | **18** | **64** | **22** |

独立窗：关于窗（1 按钮）· 剪贴板窗（4 按钮）· 翻译窗（1 按钮）· Hosts 编辑窗（2 按钮）· 输入/确认对话框（各 2 按钮）。

主菜单（3 项）：工具-（JSON/Hosts/图像/哈希/文件解锁/启动项/右键菜单/文件预览等）· 设置-（常规/语音与截屏/服务与模型）· 帮助-（官方文档/关于）。

---

## 五、有意保留差异（非缺陷）

| 项 | 说明 |
|---|------|
| `TranslatorService.Resolve` | 服务层保留了 `/api/translator/resolve-url` 的能力，但**界面无入口**——迁移前 React 也没调用它 |
| `NetworkService.IsElevated` | 属性保留；界面实际读的是快照里的 `IsElevated`，两者同源 |
| `ClipboardService.History` | 暴露底层 `HistoryService`，供未来直连 SQLite 的场景；当前界面走 `GetEntries` |
| `UtilityService.GetHosts(out view)` | 同步版保留给 `ReadHosts()` 内部复用 |
| 剪贴板图片 | 迁移前经 `/api/media/file` 传输，现在直接绑本地路径，更快也更少一层 |

---

## 五、这一轮新增的功能（工具整合）

| 项 | 说明 |
|---|------|
| 右键菜单管理（工具） | 集成 MenuManager：Win11 一级 / 完整二级菜单切换 + 「用 XXX 打开」右键菜单项增删；配置存到 `文档\FNSoftware\DevEnv\ContextMenu\menu_configs.json`，旧版 MenuManager 配置首次启动自动迁入 |
| 提权执行 | 写 `HKEY_CLASSES_ROOT` 需要管理员：DevEnv 以 `--context-menu-apply <请求文件>` 提权重启执行完即退出（与 `--edit-hosts` 同一套路） |
| 工具独立窗口 | `工具` 菜单 6 项全部改为独立非模态窗口，同一工具只开一个；主窗最小化/退到托盘时随主窗一起隐藏 |
| 系统工具目录联动 | `WindowsToolRunner` 里 5 个内置工具的 `tools/*` 激活同样改为开窗，不再切主窗内容区 |

验证：进程内探针覆盖「6 个工具开窗 + 标题正确 + 每工具单例 + 子菜单 7 项 + 配置加载 10 条 + Win11 状态读取」，全部通过。

**未能自动验证**：真实鼠标点击菜单栏（本机两块屏，合成输入已完全落不到窗口——连左侧导航点击都失效，属环境问题）。请手工点一遍「工具 → 各工具」确认。

## 五点五、已知小问题（不影响使用）

- 若在异步命令（如「开始扫描」「计算哈希」）执行过程中立刻退出程序，`AsyncRelayCommand` 会在关闭后的线程池线程上回写按钮状态并抛一次跨线程异常。命令本身已经执行完，异常只发生在退出瞬间；长驻进程无实际影响。

## 六、仍需人工确认（自动化覆盖不到）

| 项 | 期望 |
|---|------|
| 托盘双击唤醒 / 退出 / 关闭主窗进托盘 | 点关闭只隐藏，不退进程 |
| `Alt+Space` 翻译 / `Ctrl+Space` 剪贴板 / 托盘单击全局启动器 | 翻译窗填充剪贴板文本 / 剪贴板窗前置 / 全局启动器显示与收起 |
| 全局启动器框选 / 拖拽排序 / 启动器文件夹 | 点击空白不关窗；多选高亮；重启后顺序与分组保留；文件夹可展开、重命名、解散、移出条目 |
| 拖放 .exe 与文件夹进主窗 | 落到「快捷方式」窗口的对应页签并刷新 |
| 熔岩：启停服务、开始下载、活动/历史刷新、镜像测速与切换、环境扫描 | 与迁移前一致 |
| 网络/进程：结束进程、结束进程树、端口结束、测速进度 | 与迁移前一致 |
| 工具：JSON 格式化、Hosts 保存（非管理员拉起提权窗）、哈希、图像转换、启动项增删 | 与迁移前一致 |
| 设置：改配置保存后重启仍生效、检查更新 | data.json 落盘正确 |
| 深色模式逐页翻一遍 | 无「露白」控件 |

> 建议把这张表贴到一次手工回归里走一遍；本轮自动化能保证「命令接得上」，保证不了「点击后的业务结果与迁移前完全一致」。