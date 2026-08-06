# ProgramBox (devbox)

[![CI](https://github.com/pengcunfu/devbox/actions/workflows/ci.yml/badge.svg)](https://github.com/pengcunfu/devbox/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/pengcunfu/devbox?label=release)](https://github.com/pengcunfu/devbox/releases/latest)
[![Docs](https://img.shields.io/badge/docs-VitePress-blue)](https://pengcunfu.github.io/devbox/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/pengcunfu/devbox/blob/master/LICENSE)

Windows 上的开发环境管理工具：多语言运行时切换、系统工具快捷入口、本地/Web 应用与服务集中管理。

**当前版本：1.0.0** · 开源协议：[MIT](LICENSE)

## 下载

从 [Releases](https://github.com/pengcunfu/devbox/releases/latest) 下载 `ProgramBox-1.0.0-win-x64.zip`，解压后运行 `ProgramBox.exe`（64 位自包含，无需安装 .NET）。

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
2. `git tag v1.0.0 && git push origin v1.0.0`
3. GitHub Actions 自动构建 zip 并创建 Release

详见 [版本发布](https://pengcunfu.github.io/devbox/guide/releases)。

## 参与贡献

欢迎提交 Issue 与 Pull Request。仓库地址：[pengcunfu/devbox](https://github.com/pengcunfu/devbox)

## 许可证

本项目采用 [MIT License](LICENSE) 开源。
