# 版本发布

ProgramBox 通过 [GitHub Releases](https://github.com/pengcunfu/devbox/releases) 分发 Windows 64 位 zip 包，不提供 MSI/安装程序。

## 用户：如何更新

1. 打开 [Releases](https://github.com/pengcunfu/devbox/releases)
2. 下载最新 `ProgramBox-x.y.z-win-x64.zip`
3. 关闭正在运行的 ProgramBox
4. 解压覆盖原程序目录即可（用户数据在 `文档\FNSoftware\DevBox\`，不会随安装包覆盖）

## 维护者：如何发版

版本以仓库内三处为**单一事实来源**，发版前必须一致：

| 文件 | 字段 |
|------|------|
| `VERSION` | 纯文本，如 `1.0.0` |
| `DevBox/DevBox.csproj` | `<Version>`、`<AssemblyVersion>`、`<FileVersion>`、`<InformationalVersion>` |
| `CHANGELOG.md` | `## [1.0.0] - YYYY-MM-DD` |

Git 标签格式为 **`v` + 上述版本号**（例如 `v1.0.0`）。Release 工作流会校验：标签 ↔ `VERSION` ↔ `csproj` ↔ `CHANGELOG`，任一不一致则构建失败。

### 1. 更新版本与日志

1. 将 `VERSION` 改为新版本号（如 `1.0.1`）
2. 同步 `DevBox/DevBox.csproj` 中所有版本属性（`AssemblyVersion` / `FileVersion` 一般为 `X.Y.Z.0`）
3. 在 `CHANGELOG.md` 增加 `## [1.0.1] - 日期` 并写好变更说明

### 2. 本地校验（推荐）

```powershell
.\scripts\verify-version.ps1 -Tag v1.0.1
```

### 3. 提交并打标签

```bash
git add VERSION DevBox/DevBox.csproj CHANGELOG.md
git commit -m "chore: release v1.0.1"
git tag v1.0.1
git push origin master
git push origin v1.0.1
```

推送以 `v` 开头的标签（`v*.*.*`）会触发 [release.yml](https://github.com/pengcunfu/devbox/blob/master/.github/workflows/release.yml)：

1. 校验 `VERSION` / `csproj` / `CHANGELOG` 与标签一致
2. `dotnet publish`（win-x64，自包含单文件，版本号写入程序集）
3. 校验 `ProgramBox.exe` 的 `ProductVersion`
4. 打包 `ProgramBox-<semver>-win-x64.zip` 并创建 GitHub Release（附 `CHANGELOG` 对应章节）

### 4. 手动触发（可选）

Actions → **Release** → **Run workflow**，输入标签如 `v1.0.0`（须与当前 `VERSION` 文件一致）。

::: warning
手动发版前请先提交版本号与 CHANGELOG 的修改；工作流不会自动改版本文件。
:::

## 产物说明

| 文件 | 说明 |
|------|------|
| `ProgramBox-x.y.z-win-x64.zip` | 解压即用，内含 `ProgramBox.exe` 及依赖 |

当前 **不提供** win-x86、MSI、商店包；如有需求请提 [Issue](https://github.com/pengcunfu/devbox/issues)。

## CI

- **CI**：推送/PR 到 `master`/`main`/`develop` 时执行 `dotnet build`
- **Docs**：推送 `docs/**` 时部署 VitePress 到 GitHub Pages
