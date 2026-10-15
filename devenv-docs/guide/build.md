# 从源码构建

## 环境要求

- Windows 10+
- [.NET 10 SDK](https://dotnet.microsoft.com/download)（与仓库 `global.json` 一致）
- Git

## 克隆与编译

```bash
git clone https://github.com/pengcunfu/DevBox.Net.git
cd devbox

dotnet restore
dotnet build -c Release
dotnet run --project DevBox/DevBox.csproj -c Release
```

编译产物位于仓库根目录 `bin/DevBox/Release/net10.0-windows/`（各子项目的 `bin`/`obj` 统一放在根下 `bin/<项目名>/`、`obj/<项目名>/`）。

## 本地发布（单文件）

仓库提供了发布脚本，框架依赖单文件（目标机需装对应 .NET Desktop Runtime）：

```powershell
.\scripts\publish.ps1          # 默认 Release / win-x64
.\scripts\publish.ps1 -Zip     # 额外生成 DevBox-<ver>-win-x64.zip
```

等价的手动命令：

```bash
dotnet publish DevBox/DevBox.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained false ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o ./publish/win-x64
```

在 `publish/win-x64` 目录运行 `DevBox.exe`。

### 本地打包 Velopack 安装器（可选）

发布由 GitHub Actions 全自动完成；本地联调时可安装 vpk 工具链手动打包：

```bash
dotnet tool install -g vpk --version 1.2.0
vpk pack -u DevBox -v 1.2.0 -r win-x64 ^
  -p ./publish/win-x64 -o ./publish/velopack ^
  -e DevBox.exe --packTitle DevBox --packAuthors FNSoftware ^
  -i DevBox/Resources/icon.ico --framework net10-x64-desktop
```

本地全流程联调更新时，可将 `DEVBOX_UPDATE_FEED` 环境变量指向本地目录或 HTTP 地址，替代 GitHub Releases 作为更新源。

## 修改版本号

发布前请同步以下位置：

1. 根目录 `VERSION`
2. `DevBox/DevBox.csproj` 中的 `<Version>` 等属性
3. `CHANGELOG.md` 对应版本条目

应用内版本从程序集信息读取，无需改 XAML 硬编码。

## 文档站点（VitePress → Cloudflare Pages）

项目名：`devbox-docs` · 线上地址：**https://docs.devenv.cn/**（备用 `https://devbox-docs.pages.dev/`）

```bash
cd docs
npm install
npm run dev       # 本地预览 http://localhost:5173/
npm run build     # 输出到 docs/dist
npm run deploy    # 构建并部署（等价于 .\deploy.ps1）
```

::: tip 本地一键部署
`docs/deploy.ps1` 会检查 Cloudflare 登录（未登录自动 `wrangler login`）、必要时创建 Pages 项目，再部署 `dist/`。也可使用 API Token：设置 `CLOUDFLARE_API_TOKEN`（建议同时设置 `CLOUDFLARE_ACCOUNT_ID`）。
:::

推送至 `master`/`main` 且变更 `docs/**` 时，GitHub Actions 会调用 wrangler 部署到同一 Pages 项目。仓库需配置 Secrets：

- `CLOUDFLARE_API_TOKEN`（Cloudflare Pages: Edit）
- `CLOUDFLARE_ACCOUNT_ID`
