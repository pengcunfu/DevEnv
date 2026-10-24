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
| `website/` | Vue 3 + Vite 官网与文档站（首页 + `guide/**/*.md` 文档 + `/downloads` 软件下载站，正文由 markdown-it 渲染；下载站数据在 `src/data/software.json`）→ Cloudflare Pages，项目名 `devbox-docs`，站点 https://docs.devenv.cn/ |
| `docs/` | 子项目说明文档（纯 Markdown，**不是**文档站，别往里放构建配置） |
| `scripts/` | 发布与版本校验的 PowerShell 脚本 |

**功能模块是 `DevEnv.Core` / `DevEnv` 工程内的子目录，不再有独立工程**。新功能按 `DevEnv.Core/<模块>/{Models,Services}` + `DevEnv/{Views,ViewModels,Windows}` 放置，命名空间与目录一致（`DevEnv.Core.<模块>.*` / `DevEnv.Host.*`）。不要恢复独立 `Program.cs`、独立 `sln` 或独立入口。

## 常用命令

```bash
dotnet build DevEnv.sln -c Debug # 构建全部；产物在各项目的 bin/、obj/ 下
make run                          # 从源码跑主程序（Release，WPF 宿主）
make test                         # 跑单元测试（DevEnv.Tests，xunit）
make verify                       # 校验 VERSION / csproj / CHANGELOG 三处一致
make publish                      # 单文件发布到 publish/win-x64/
make installer                    # publish + vpk pack，产出 Velopack Setup.exe
cd website && npm run dev         # 文档站本地预览
cd website && npm run deploy      # 文档站构建并部署
```

调试主程序时可用环境变量直接启动到指定页：`DEVENV_START_PAGE=lava make run`（导航 id 见 `Views/ViewFactory.cs`）。

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
