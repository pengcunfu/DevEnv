# Changelog

本文件记录各版本变更。发布新版本时请在此补充条目，并打 Git 标签（如 `v1.0.1`）触发 Release 工作流。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。

## [Unreleased]

## [1.1.0] - 2026-09-03

### Added

- Velopack 安装器与增量自更新（设置页「检查更新」，GitHub Releases 作更新源）
- 「开发环境」Tab：以进程模式启动 / 停止本地数据库与缓存服务（MySQL / Redis / PostgreSQL）
- 应用图标更新为青色工具箱设计

### Changed

- 发布方式由 zip 包改为 `DevBox-win-Setup.exe` 安装器（框架依赖单文件，安装器自动安装缺失的 .NET 10 Desktop Runtime）
- 安装后支持应用内自动更新，不再需要手动解压覆盖

## [1.0.0] - 2026-05-24

### Added

- 首个正式版本：Windows 64 位 zip 发布
- 开发环境管理（多语言运行时与版本切换）
- 系统工具快捷入口
- 本地应用、Web 应用与服务管理
- VitePress 在线文档与 GitHub Pages 部署
- MIT 开源协议

[Unreleased]: https://github.com/pengcunfu/devbox/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/pengcunfu/devbox/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/pengcunfu/devbox/releases/tag/v1.0.0
