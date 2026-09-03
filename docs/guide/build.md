# 从源码构建

## 环境要求

- Windows 10+
- [.NET 10 SDK](https://dotnet.microsoft.com/download)（与仓库 `global.json` 一致）
- Git

## 克隆与编译

```bash
git clone https://github.com/pengcunfu/devbox.git
cd devbox

dotnet restore
dotnet build -c Release
dotnet run --project DevBox/DevBox.csproj -c Release
```

编译产物位于 `DevBox/bin/Release/net10.0-windows/`。

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
vpk pack -u DevBox -v 1.1.0 -r win-x64 ^
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

## 文档站点（VitePress）

```bash
cd docs
npm install
npm run dev      # 本地预览 http://localhost:5173/devbox/
npm run build    # 输出到 docs/.vitepress/dist
```

推送至 `master` 且变更 `docs/**` 时，GitHub Actions 会自动部署到 GitHub Pages。

::: info GitHub Pages 首次启用
仓库 [Settings → Pages](https://github.com/pengcunfu/devbox/settings/pages) 中，**Build and deployment** 选择 **GitHub Actions**。
:::

在线文档地址：**https://pengcunfu.github.io/devbox/**
