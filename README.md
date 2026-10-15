# DevBox

[![Release](https://github.com/pengcunfu/DevBox.Net/actions/workflows/release.yml/badge.svg)](https://github.com/pengcunfu/DevBox.Net/actions/workflows/release.yml)
[![Version](https://img.shields.io/github/v/release/pengcunfu/DevBox.Net?label=release)](https://github.com/pengcunfu/DevBox.Net/releases/latest)
[![Docs](https://img.shields.io/badge/docs-docs.devenv.cn-blue)](https://docs.devenv.cn/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/pengcunfu/DevBox.Net/blob/master/LICENSE)

Windows 上的开发环境管理工具：应用与服务集中管理、熔岩绿色版环境、熔岩网页翻译、网络监控、熔岩剪贴板、系统工具快捷入口。

**当前版本：1.1.0** · 开源协议：[MIT](LICENSE)

## 下载与更新

从 [Releases](https://github.com/pengcunfu/DevBox.Net/releases/latest) 下载 `DevBox-win-Setup.exe` 安装包并安装（若缺少 .NET 10 Desktop Runtime，安装器会自动安装）。安装后可在设置页「检查更新」自动升级，无需手动下载覆盖。

## 文档

完整使用与发版说明见在线文档：**https://docs.devenv.cn/**

本地预览 / 部署文档：

```bash
cd docs
npm install
npm run dev       # 本地预览
npm run deploy    # 构建并部署到 Cloudflare Pages（首次会浏览器登录）
```

## 技术栈

- .NET 10 · Avalonia UI 11
- Windows 10+（x64）

## 从源码运行

```bash
git clone https://github.com/pengcunfu/DevBox.Net.git
cd devbox
dotnet run --project DevBox/DevBox.csproj -c Release
```

## 发版（维护者）

1. 更新 `VERSION`、`DevBox/DevBox.csproj`、`CHANGELOG.md`
2. `git tag v1.1.0 && git push origin v1.1.0`
3. GitHub Actions 自动构建安装包 / 更新包 / 便携包并创建 Release

详见 [版本发布](https://docs.devenv.cn/guide/releases)。

## 参与贡献

欢迎提交 Issue 与 Pull Request。仓库地址：[pengcunfu/DevBox.Net](https://github.com/pengcunfu/DevBox.Net)

## 许可证

本项目采用 [MIT License](LICENSE) 开源。
