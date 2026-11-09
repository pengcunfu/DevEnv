# 版本发布

DevEnv 通过 [GitHub Releases](https://github.com/pengcunfu/DevEnv/releases) 以 **便携 zip + 自研自更新** 分发 Windows 64 位版本。

## 用户：如何安装与更新

### 首次安装

1. 打开 [Releases](https://github.com/pengcunfu/DevEnv/releases)
2. 下载 `DevEnv-<ver>-win-x64.zip`
3. 解压到任意可写目录（例如 `D:\Tools\DevEnv`），运行 `DevEnv.exe`
4. 目标机需要安装 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)；程序本身不携带运行时

> 旧版 Velopack 安装（`%LOCALAPPDATA%\DevEnv\current\`）不能原地升级到便携版，请下载新版 zip 做一次性迁移；旧安装的 `文档\FNSoftware\DevEnv` 用户数据会继续复用。

### 升级

- **应用内自动更新（推荐）**：打开「设置 → 关于与帮助」→「检查更新」，发现新版本后点「下载更新」，完成后点「重启并应用」。更新器下载新版 zip、校验 SHA256、退出旧进程后替换程序目录；新版启动失败会自动回滚到上一版本。
- **手动升级**：下载新版 zip，退出程序后解压覆盖原目录即可。

### 可选组件

基础包只带 Magick.NET 等核心依赖。Office 结构化预览、截图 OCR 与语音输入所需引擎在第一次使用或「设置 → 服务与模型」中按需下载：

| 组件 | 下载大小 | 安装后 | 用途 |
|------|------|------|------|
| Office 预览 | ~2 MB | ~8.5 MB | docx / xlsx / pptx 结构化解析 |
| OCR 引擎 | ~12 MB | ~30 MB | 截图 OCR（含 onnxruntime） |
| 语音引擎 | ~8 MB | ~21 MB | SenseVoice 语音输入（含 onnxruntime） |

模型文件（OCR ~30 MB、SenseVoice ~230 MB）仍单独下载到 `文档\FNSoftware\SigXor\Models`；组件安装在 `%LOCALAPPDATA%\DevEnv\Components`。

用户数据保存在 `文档\FNSoftware\DevEnv\`，升级不会丢失。

## 维护者：如何发版

版本以仓库内三处为**单一事实来源**，发版前必须一致：

| 文件 | 字段 |
|------|------|
| `VERSION` | 纯文本，如 `1.3.0` |
| `DevEnv/DevEnv.csproj` | `<Version>`、`<AssemblyVersion>`、`<FileVersion>`、`<InformationalVersion>` |
| `CHANGELOG.md` | `## [1.3.0] - YYYY-MM-DD` |

Git 标签格式为 **`v` + 上述版本号**（例如 `v1.3.0`）。Release 工作流会校验：标签 ↔ `VERSION` ↔ `csproj` ↔ `CHANGELOG`，任一不一致则构建失败。

### 1. 更新版本与日志

1. 将 `VERSION` 改为新版本号（如 `1.3.1`）
2. 同步 `DevEnv/DevEnv.csproj` 中所有版本属性（`AssemblyVersion` / `FileVersion` 一般为 `X.Y.Z.0`）
3. 在 `CHANGELOG.md` 增加 `## [1.3.1] - 日期` 并写好变更说明

### 2. 本地校验（推荐）

```powershell
.\scripts\verify-version.ps1 -Tag v1.3.1
```

### 3. 本地打包（可选）

```powershell
make package
```

产物在 `publish/release/`：便携 app zip、3 个组件 zip、`latest.json`、`components.json` 与 SHA256。发布脚本内部先做瘦身单文件发布，再做一次完整发布提取组件运行时文件。

### 4. 提交并打标签

```bash
git add VERSION DevEnv/DevEnv.csproj CHANGELOG.md
git commit -m "杂项: 版本号升至 1.3.1"
git tag v1.3.1
git push origin master
git push origin v1.3.1
```

推送以 `v` 开头的标签（`v*.*.*`）会触发 [release.yml](https://github.com/pengcunfu/DevEnv/blob/master/.github/workflows/release.yml)：

1. 校验 `VERSION` / `csproj` / `CHANGELOG` 与标签一致
2. 跑单元测试
3. `scripts/package-release.ps1`：`--no-self-contained` 单文件发布 + 完整发布 + 组件打包 + 便携 zip + `latest.json`
4. 校验产物（exe < 70 MB、3 个组件 zip、清单字段齐全）
5. `gh release create` 上传到 GitHub Release（tag 触发）；`workflow_dispatch` 手动触发仅上传 artifact，不发布

### 5. 手动触发（可选）

Actions → **Release** → **Run workflow**，输入标签如 `v1.3.1`（须与当前 `VERSION` 文件一致）。

::: warning
手动发版前请先提交版本号与 CHANGELOG 的修改；工作流不会自动改版本文件。
:::

## 产物说明

每次 Release 包含：

| 文件 | 说明 |
|------|------|
| `DevEnv-<ver>-win-x64.zip` | 便携程序包（exe + Resources + 许可证），解压即用 |
| `DevEnv-<ver>-win-x64.zip.sha256` | 便携包 SHA256 |
| `DevEnv-component-office-<ver>.zip` | Office 结构化预览组件 |
| `DevEnv-component-ocr-<ver>.zip` | OCR 引擎组件（含 onnxruntime） |
| `DevEnv-component-voice-<ver>.zip` | 语音引擎组件（含 onnxruntime） |
| `latest.json` | 自更新清单（版本 / 下载地址 / SHA256 / 大小） |
| `components.json` | 可选组件清单（3 个组件的地址与哈希） |

当前 **不提供** win-x86、MSI、商店包；如有需求请提 [Issue](https://github.com/pengcunfu/DevEnv/issues)。

## CI

- **Release**：推送 `v*.*.*` 标签时自动构建并发布
- **Docs**：不经过 CI，本机执行 `make deploy-docs`（或 `website/` 下 `npm run deploy`）发布到 Cloudflare Pages（`devenv.cn`）
