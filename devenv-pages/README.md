# 开发环境下载站

<div align="center">

![Vue](https://img.shields.io/badge/Vue-3.x-42b883.svg)
![Vite](https://img.shields.io/badge/Vite-7.x-646cff.svg)
![Platform](https://img.shields.io/badge/Platform-Cloudflare%20Pages-orange.svg)

**常用开发环境与工具安装包下载导航（国内镜像加速）** · 点击链接直接下载

[在线访问](https://devenv-download.pages.dev) • [数据维护](docs/DATA_FORMAT.md)

</div>

---

## 项目简介

这是一个**静态软件下载站**：把常用的开发环境/工具安装包按分类列出，点击「下载」按钮即可跳转到对应地址直接下载（外链，站点不托管任何文件）。下载链接优先使用**国内镜像**（清华 TUNA、阿里云等），保证国内高速下载。

**核心工作流：你只需维护一份 `src/data/software.json` 链接列表，改 JSON → 构建 → 上线。**

## 功能特性

- **分类导航**：Java / Python / PHP / Node.js / WebServer / Database / Cache / DBTools
- **软件卡片**：版本下拉选择、来源徽章（清华镜像/官网/GitHub）、官网入口、复制链接
- **实时搜索**：按软件名/描述/分类名过滤
- **响应式**：桌面与移动端自适应，支持暗色模式
- **国内加速**：下载源优先国内镜像

## 快速开始

### 环境要求

- Node.js 20.19+ / 22.12+（含 npm）

### 本地开发

```bash
npm install      # 安装依赖
npm run dev      # 启动开发服务器 http://localhost:5173
```

### 构建

```bash
npm run build    # 产物输出到 dist/
npm run preview  # 本地预览生产构建 http://localhost:4173
```

### 部署到 Cloudflare Pages

```bash
npm run deploy   # 构建 + 部署（需已登录 wrangler / 设置 CLOUDFLARE_API_TOKEN）
```

部署后访问 `https://devenv-download.pages.dev`。

## 数据维护（核心）

**所有站点内容都在 `src/data/software.json`，一个文件搞定。**

- 完整字段说明与操作教程见 [`docs/DATA_FORMAT.md`](docs/DATA_FORMAT.md)
- 用 VS Code 编辑会自动校验（已关联 `schema/software.schema.json`，漏字段/URL 不合法会有红色波浪线 + 中文提示）
- 快速校验：`python -m json.tool src/data/software.json`

### 链接检查

下载链接可能随时间失效，建议定期检查：

```bash
python scripts/check_links.py   # 检查所有链接可达性（HEAD 请求）
```

新增/修改链接后，建议抽查国内可达性（可用 `curl -I <url>`）。

## 项目结构

```
devenv-download/
├── src/
│   ├── data/software.json   # 站点数据源（维护它即可）
│   ├── App.vue              # 顶层布局 + 搜索过滤
│   ├── components/          # 页头/卡片/分类/徽章/图标/页脚组件
│   └── style.css            # 全局样式
├── schema/software.schema.json  # 数据校验 Schema（编辑器提示）
├── docs/DATA_FORMAT.md          # 数据格式说明
├── scripts/
│   └── check_links.py       # 链接可达性检查
└── deploy.ps1               # Cloudflare Pages 部署脚本
```

## 技术栈

- [Vue 3](https://vuejs.org/) + [Vite](https://vitejs.dev/)（JavaScript，无 TS）
- 部署：[Cloudflare Pages](https://pages.cloudflare.com/)（wrangler CLI）
- 无后端、无构建时请求，纯静态托管

## 版权

© FireNeb · [fireneb.com](https://fireneb.com)

---

<div align="center">

**如果这个站对你有帮助，欢迎收藏分享！**

[FireNeb 官网](https://fireneb.com)

</div>
