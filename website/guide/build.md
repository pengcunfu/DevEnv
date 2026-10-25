# 从源码构建

## 环境要求

- Windows 10+
- [.NET 10 SDK](https://dotnet.microsoft.com/download)（使用本机安装的 SDK，不通过 `global.json` 固定版本）
- Git

界面是原生 WPF，构建与运行不需要 Node.js。只有克隆文档站并本地预览时才需要 Node.js 20+。

## 克隆与运行（开发）

```bash
git clone https://github.com/pengcunfu/DevEnv.git
cd DevEnv

dotnet restore
dotnet build DevEnv.sln -c Debug

# 启动主程序（WPF）
make run CONFIG=Debug
```

编译产物位于各项目自己的 `bin/`、`obj/` 目录，例如 `DevEnv/bin/Debug/net10.0-windows/DevEnv.exe`、`DevEnv.Core/bin/Debug/net10.0-windows/`。`make clean` 会清理各项目产物，并兼容清理旧的仓库根目录 `bin/`、`obj/` 残留。

设置 `DarkMode` 为 true（或在「设置」对话框中勾选深色模式）即可切换深色主题。调试时可用环境变量直接启动到指定页：`DEVENV_START_PAGE=lava make run`。

## 本地发布（单文件）

仓库提供了发布脚本：框架依赖单文件发布（目标机需装对应 .NET Desktop Runtime）：

```powershell
.\scripts\publish.ps1          # 默认 Release / win-x64
.\scripts\publish.ps1 -Zip     # 额外生成 DevEnv-<ver>-win-x64.zip
```

等价的手动流程：

```bash
dotnet publish DevEnv/DevEnv.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained false ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o ./publish/win-x64
```

在 `publish/win-x64` 目录运行 `DevEnv.exe`。

### 本地打包 Velopack 安装器（可选）

发布由 GitHub Actions 全自动完成；本地联调时可安装 vpk 工具链手动打包：

```bash
dotnet tool install -g vpk --version 1.2.0
vpk pack -u DevEnv -v 1.2.0 -r win-x64 ^
  -p ./publish/win-x64 -o ./publish/velopack ^
  -e DevEnv.exe --packTitle DevEnv --packAuthors FireNeb ^
  -i DevEnv/Resources/icon.ico --framework net10-x64-desktop
```

本地全流程联调更新时，可将 `DEVBOX_UPDATE_FEED` 环境变量指向本地目录或 HTTP 地址，替代 GitHub Releases 作为更新源。

## 修改版本号

发布前请同步以下位置：

1. 根目录 `VERSION`
2. `DevEnv/DevEnv.csproj` 中的 `<Version>` 等属性
3. `CHANGELOG.md` 对应版本条目

应用内版本从程序集信息读取。

## 文档站点（Vue + Vite → Cloudflare Pages）

项目名：`devbox-docs` · 线上地址：**https://devenv.cn/**（备用 `https://devbox-docs.pages.dev/`）

站点首页是 Vue 组件，指南正文仍以 Markdown 维护在 `website/guide/`，由 `markdown-it` 渲染，不再使用 VitePress。

```bash
cd website
npm install
npm run dev       # 本地预览（Vite 开发服务器）
npm run build     # 预渲染静态页面到 website/dist
npm run preview   # 本地预览构建产物
npm run deploy    # 构建并部署（等价于 .\deploy.ps1）
```

::: tip 本地一键部署
`website/deploy.ps1` 会检查 Cloudflare 登录（未登录自动 `wrangler login`）、必要时创建 Pages 项目，再部署 `dist/`。也可使用 API Token：设置 `CLOUDFLARE_API_TOKEN`（建议同时设置 `CLOUDFLARE_ACCOUNT_ID`）。
:::

推送至 `master`/`main` 且变更 `website/**` 时，GitHub Actions 会调用 wrangler 部署到同一 Pages 项目。仓库需配置 Secrets：

- `CLOUDFLARE_API_TOKEN`（Cloudflare Pages: Edit）
- `CLOUDFLARE_ACCOUNT_ID`
