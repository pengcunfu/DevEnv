# 版本发布

DevEnv 通过 [GitHub Releases](https://github.com/pengcunfu/DevBox.Net/releases) 以 **Velopack 安装器 + 自更新** 分发 Windows 64 位版本。

## 用户：如何安装与更新

### 首次安装

1. 打开 [Releases](https://github.com/pengcunfu/DevBox.Net/releases)
2. 下载 `DevEnv-win-Setup.exe`
3. 运行安装器（缺少 .NET 10 Desktop Runtime 时安装器会自动安装）

### 升级

安装后有两种升级方式：

- **应用内自动更新（推荐）**：打开「设置 → 更新」→「检查更新」，发现新版本后点「下载并更新」，完成后点「立即重启更新」即自动升级（增量下载，更快）。
- **重新安装**：下载新版 `DevEnv-win-Setup.exe` 覆盖安装即可。

用户数据保存在 `文档\FNSoftware\DevEnv\`，升级不会丢失。

## 维护者：如何发版

版本以仓库内三处为**单一事实来源**，发版前必须一致：

| 文件 | 字段 |
|------|------|
| `VERSION` | 纯文本，如 `1.2.0` |
| `DevEnv/DevEnv.csproj` | `<Version>`、`<AssemblyVersion>`、`<FileVersion>`、`<InformationalVersion>` |
| `CHANGELOG.md` | `## [1.2.0] - YYYY-MM-DD` |

Git 标签格式为 **`v` + 上述版本号**（例如 `v1.2.0`）。Release 工作流会校验：标签 ↔ `VERSION` ↔ `csproj` ↔ `CHANGELOG`，任一不一致则构建失败。

### 1. 更新版本与日志

1. 将 `VERSION` 改为新版本号（如 `1.2.1`）
2. 同步 `DevEnv/DevEnv.csproj` 中所有版本属性（`AssemblyVersion` / `FileVersion` 一般为 `X.Y.Z.0`）
3. 在 `CHANGELOG.md` 增加 `## [1.2.1] - 日期` 并写好变更说明

### 2. 本地校验（推荐）

```powershell
.\scripts\verify-version.ps1 -Tag v1.1.1
```

### 3. 提交并打标签

```bash
git add VERSION DevEnv/DevEnv.csproj CHANGELOG.md
git commit -m "chore: release v1.1.1"
git tag v1.1.1
git push origin master
git push origin v1.1.1
```

推送以 `v` 开头的标签（`v*.*.*`）会触发 [release.yml](https://github.com/pengcunfu/DevBox.Net/blob/master/.github/workflows/release.yml)：

1. 校验 `VERSION` / `csproj` / `CHANGELOG` 与标签一致
2. `dotnet publish`（win-x64，框架依赖单文件）并校验 `DevEnv.exe` 的 `ProductVersion`
3. `vpk download` 拉取上一版本 → `vpk pack` 生成安装包 / 增量更新包 / 便携包 / 更新清单
4. `vpk upload` 上传到 GitHub Release（tag 触发）；`workflow_dispatch` 手动触发仅构建产物，不发布

### 4. 手动触发（可选）

Actions → **Release** → **Run workflow**，输入标签如 `v1.1.1`（须与当前 `VERSION` 文件一致）。

::: warning
手动发版前请先提交版本号与 CHANGELOG 的修改；工作流不会自动改版本文件。
:::

## 产物说明

每次 Release 包含：

| 文件 | 说明 |
|------|------|
| `DevEnv-win-Setup.exe` | 安装器（推荐；首次安装自动装 .NET 10 Desktop Runtime） |
| `DevEnv-<ver>-win-x64.nupkg` | 完整更新包（增量更新回退用） |
| `DevEnv-<ver>-delta.nupkg` | 增量更新包（自更新默认下载） |
| `DevEnv-win-Portable.zip` | 免安装便携包（需手动解压，无自动更新） |
| `RELEASES` | 更新清单（自更新客户端读取） |

当前 **不提供** win-x86、MSI、商店包；如有需求请提 [Issue](https://github.com/pengcunfu/DevBox.Net/issues)。

## CI

- **Release**：推送 `v*.*.*` 标签时自动构建并发布
- **Docs**：推送 `website/**` 时部署 VitePress 到 Cloudflare Pages（`docs.devenv.cn`）
