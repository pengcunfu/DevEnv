# AGENTS.md

给在本仓库工作的 AI 编码助手（Claude Code / Cursor / Codex 等）的说明，人类贡献者同样适用。
写代码前先读这一页；与 `Makefile`、`scripts/verify-version.ps1` 冲突时，以脚本的实际校验逻辑为准。

## 项目是什么

**DevBox**（产品显示名「程序盒子」）是 Windows 桌面工具：.NET 10 + Avalonia 12，把开发环境管理和几个自研小工具收进同一个壳，主窗用左侧 Tab 切换。解决方案文件是 `DevEnv.sln`。

| 路径 | 角色 |
|------|------|
| `DevBox/` | 主程序（`WinExe`）：主窗与左侧 Tab、设置、托盘、Velopack 安装与自更新 |
| `DevEnv/` | 熔岩环境模块（库）：绿色版进程管理、软件下载、环境扫描、镜像配置；JSON / Hosts / 图片转换 / 文件哈希等窗口也由本模块提供，入口在 DevBox「系统工具」 |
| `Translator/Translator/` | 熔岩翻译模块（库）：WebView2 多站点网页翻译，`Alt+Space` 唤起 |
| `NetworkWatch/` | 网络监控模块（库）：按进程查看连接与实时速率、端口管理、一键测速 |
| `Clipboard/` | 熔岩剪贴板模块（库）：剪贴板历史，SQLite 持久化 |
| `WinBootSelfStarting/` | 启动项管理的早期独立实现（WPF / net8.0），功能已由 DevBox 的启动项管理窗口承接，不在 `DevEnv.sln` 内 |
| `devenv-docs/` | VitePress 在线文档站 → Cloudflare Pages，项目名 `devbox-docs`，站点 https://docs.devenv.cn/ |
| `devenv-pages/` | 开发环境下载站（Vue 3 + Vite）→ Cloudflare Pages，项目名 `devenv` |
| `docs/` | 子项目说明文档（纯 Markdown，**不是**文档站，别往里放构建配置） |
| `scripts/` | 发布与版本校验的 PowerShell 脚本 |

**内嵌模块一律以 `Library` 形式被 DevBox 引用**，各自的独立入口在 csproj 里用 `Compile Remove` / `AvaloniaXaml Remove` 停用。新增文件时不要把这些入口恢复回来，也不要新增独立的 `Program.cs` / `App.axaml`。

## 常用命令

```bash
dotnet build DevEnv.sln -c Debug      # 构建全部；产物在仓库根 bin/<项目名>/...
make run                              # 从源码跑主程序（Release）
make verify                           # 校验 VERSION / csproj / CHANGELOG 三处一致
make publish                          # 单文件发布到 publish/win-x64/
make installer                        # publish + vpk pack，产出 Velopack Setup.exe
cd devenv-docs && npm run dev         # 文档站本地预览
cd devenv-docs && npm run deploy      # 文档站构建并部署
```

## 硬性约定

1. **提交信息用中文**，格式为「`类型: 标题`」加详细正文（正文逐条写清改了什么、为什么、影响范围）。类型沿用仓库历史：`功能:` `修复:` `重构:` `性能:` `文档:` `样式:` `测试:` `杂项:`。
2. **提交信息里绝不能出现 AI 署名或生成标记**——`Co-Authored-By:`、`Generated with ...`、`Signed-off-by:` 一律不加，无论用哪个工具执行。工具默认会加，必须手动去掉。
3. **`Makefile` 必须保持纯 ASCII**。GNU Make 在 Windows 上会转码 makefile 里的非 ASCII 文本，UTF-8 注释和字符串在 `make help` 里会变成乱码。自检：`LC_ALL=C grep -c '[^ -~\t]' Makefile` 结果必须是 `0`。中文说明写到脚本或文档里，不要写进 Makefile。
4. **版本号有三处单一事实来源，必须同改**：`VERSION`、`DevBox/DevBox.csproj`（`Version` / `AssemblyVersion` / `FileVersion` / `InformationalVersion`，后两者形如 `X.Y.Z.0`）、`CHANGELOG.md` 里的 `## [x.y.z] - 日期`。改完跑 `make verify`；CI 会重复校验同一件事，不一致就构建失败。
5. **不要改回子项目的 `bin`/`obj`**。根 `Directory.Build.props` 已把两者统一到 `bin/<项目名>/`、`obj/<项目名>/`，别在任何 csproj 里再覆盖 `BaseOutputPath` / `BaseIntermediateOutputPath`。
6. **产品显示名与数据目录名不是一回事**：界面文案用「程序盒子」，但程序集名、命名空间和用户数据目录固定为 `DevBox`（`文档\FNSoftware\DevBox`）。改文案时不要顺手改后者，否则升级后用户数据会“丢失”。
7. **构建产物不入库**。`devenv-docs/` 和 `devenv-pages/` 各自的 `.gitignore` 已忽略 `node_modules`、`dist` 等；`DevEnv/Resources/tools/7zr.exe` 是**故意入库**的例外（缺了它，CI 全新克隆构建出的安装包无法解压 7z / tar.gz）。
8. **内嵌模块的全局热键跟随宿主生命周期**：主窗句柄就绪后调用 `EnsureHotkey()` 注册，窗口关闭时调用 `DisposeHotkey()` 释放；切换内容视图时也要补注册。改动热键相关代码时保持这个配对。
9. **改功能就同步文档**：模块级改动更新 `devenv-docs/guide/` 下对应指南与 `CHANGELOG.md`；发版流程见 `devenv-docs/guide/releases.md`。

## 已知遗留

动到相关代码时顺手修掉，不要默默绕过：

- `.github/workflows/docs.yml` 仍以 `docs/**` 触发、`working-directory: docs`，而文档站已迁到 `devenv-docs/`；`Makefile` 的 `docs` 目标同样还是 `cd docs`。两者都需要改指 `devenv-docs/`。
- `docs/Clipboard.md` 内有两处失效引用：正文的 `cd Clipboard.Net`（该目录不存在）、结尾指向已删除的 `.github/workflows/build-release.yml`。
- `DevBox/Utils/WindowResizeHelper.cs` 的注释仍提到已移除的「贴边悬停」行为。
