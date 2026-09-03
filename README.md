# DevBox

[![Release](https://github.com/pengcunfu/devbox/actions/workflows/release.yml/badge.svg)](https://github.com/pengcunfu/devbox/actions/workflows/release.yml)
[![Version](https://img.shields.io/github/v/release/pengcunfu/devbox?label=release)](https://github.com/pengcunfu/devbox/releases/latest)
[![Docs](https://img.shields.io/badge/docs-VitePress-blue)](https://pengcunfu.github.io/devbox/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/pengcunfu/devbox/blob/master/LICENSE)

Windows 上的开发环境管理工具：多语言运行时切换、系统工具快捷入口、本地/Web 应用与服务集中管理。

**当前版本：1.1.0** · 开源协议：[MIT](LICENSE)

## 下载与更新

从 [Releases](https://github.com/pengcunfu/devbox/releases/latest) 下载 `DevBox-win-Setup.exe` 安装包并安装（若缺少 .NET 10 Desktop Runtime，安装器会自动安装）。安装后可在设置页「检查更新」自动升级，无需手动下载覆盖。

## 文档

完整使用与发版说明见在线文档：**https://pengcunfu.github.io/devbox/**

本地预览文档：

```bash
cd docs
npm install
npm run dev
```

## 技术栈

- .NET 10 · Avalonia UI 11
- Windows 10+（x64）

## 从源码运行

```bash
git clone https://github.com/pengcunfu/devbox.git
cd devbox
dotnet run --project DevBox/DevBox.csproj -c Release
```

## 发版（维护者）

1. 更新 `VERSION`、`DevBox/DevBox.csproj`、`CHANGELOG.md`
2. `git tag v1.1.0 && git push origin v1.1.0`
3. GitHub Actions 自动构建安装包 / 更新包 / 便携包并创建 Release

详见 [版本发布](https://pengcunfu.github.io/devbox/guide/releases)。

## 参与贡献

欢迎提交 Issue 与 Pull Request。仓库地址：[pengcunfu/devbox](https://github.com/pengcunfu/devbox)

## 许可证

本项目采用 [MIT License](LICENSE) 开源。
