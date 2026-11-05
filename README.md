# 燧岚开发盒子 · DevEnv

FireNeb 出品的 Windows 开发环境管理工具，中文名「燧岚开发盒子」，英文名保持 **DevEnv**，官网/文档域名 **devenv.cn**。

[![Release](https://github.com/pengcunfu/DevEnv/actions/workflows/release.yml/badge.svg)](https://github.com/pengcunfu/DevEnv/actions/workflows/release.yml)
[![Version](https://img.shields.io/github/v/release/pengcunfu/DevEnv?label=release)](https://github.com/pengcunfu/DevEnv/releases/latest)
[![Docs](https://img.shields.io/badge/docs-devenv.cn-blue)](https://devenv.cn/)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://github.com/pengcunfu/DevEnv/blob/master/LICENSE)

Windows 上的开发环境管理工具：应用与服务集中管理、熔岩绿色版环境、熔岩网页翻译、网络监控、进程监控、熔岩剪贴板、文件解锁与粉碎、系统工具快捷入口。

**当前版本：1.2.0** · 品牌：[FireNeb](https://fireneb.com) · 开源协议：[Apache License 2.0](LICENSE)

## 下载与更新

从 [Releases](https://github.com/pengcunfu/DevEnv/releases/latest) 下载 `DevEnv-win-Setup.exe` 安装包并安装（若缺少 .NET 10 Desktop Runtime，安装器会自动安装）。安装后可打开「设置」对话框，在「检查更新」中自动升级，无需手动下载覆盖。

## 文档

完整使用与发版说明见在线文档：**https://devenv.cn/**

本地预览 / 部署文档：

```bash
cd website
npm install
npm run dev       # 本地预览
npm run deploy    # 构建并部署到 Cloudflare Pages（首次会浏览器登录）
```

## 技术栈

- .NET 10 · WPF 界面（仅熔岩翻译窗内嵌 WebView2）
- Windows 10+（x64）

## 从源码运行

```bash
git clone https://github.com/pengcunfu/DevEnv.git
cd DevEnv

make build                # 构建整个解决方案（DevEnv.sln，Release）
make run                  # 编译并运行主程序（Release，增量编译后启动）
make test                 # 服务层单元测试（DevEnv.Tests，Release）
```

界面是原生 WPF，构建与运行都不需要 Node.js。

## 发版（维护者）

1. 更新 `VERSION`、`DevEnv/DevEnv.csproj`、`CHANGELOG.md`
2. `git tag v1.2.0 && git push origin v1.2.0`
3. GitHub Actions 自动构建安装包 / 更新包 / 便携包并创建 Release

详见 [版本发布](https://devenv.cn/guide/releases)。

## 参与贡献

欢迎提交 Issue 与 Pull Request。仓库地址：[pengcunfu/DevEnv](https://github.com/pengcunfu/DevEnv)

## 许可证

本项目采用 [Apache License 2.0](LICENSE) 开源，版权与归属声明见 [NOTICE](NOTICE)。
