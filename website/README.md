# DevEnv 官网与文档站

燧岚开发盒子（DevEnv，FireNeb）的官网、使用文档与开发环境下载站。Vue 3 + Vite 构建，静态部署到 Cloudflare Pages，线上地址 [devenv.cn](https://devenv.cn/)。

## 站点构成

| 路径 | 内容 |
|------|------|
| `/` | 首页：产品介绍、功能模块、快速上手与下载入口 |
| `/guide/**` | 使用文档，正文为 `guide/**/*.md`（由 `src/docs/markdown.ts` 渲染） |
| `/downloads` | **开发环境下载站**：Java / Python / Node.js / 数据库 / 编辑器等常用工具安装包导航，数据来自 `src/data/software.json` |

下载站支持分类筛选、实时搜索、版本切换、来源徽章（国内镜像 / 官网 / GitHub）、复制下载链接，并跟随站点深浅色主题。

## 快速开始

```bash
npm install
npm run dev       # 本地开发 http://localhost:5173
npm run build     # 构建（vite-ssg 预渲染 + sitemap / 404 生成）
npm run preview   # 预览生产构建
npm run deploy    # 构建并部署到 Cloudflare Pages
```

## 下载站数据维护

**所有下载内容都在 `src/data/software.json`，改 JSON 即可，无需改组件。**

- 字段说明与操作教程：[`docs/DATA_FORMAT.md`](docs/DATA_FORMAT.md)
- 编辑器校验：数据文件已关联 `schema/software.schema.json`，VS Code 会提示漏字段与非法 URL
- 链接检查：`python scripts/check_links.py`（HEAD 请求，按 200 / 404 / 其他错误汇总）

新增分类或软件后运行 `npm run dev`，打开 `/downloads` 确认展示正常。

## 目录结构

```
website/
├── src/
│   ├── data/software.json        # 下载站数据源（维护它即可）
│   ├── downloads/software.ts     # 数据类型、统计与锚点工具
│   ├── components/downloads/     # 软件卡片、分类区块、来源徽章、图标
│   ├── views/DownloadsView.vue   # 下载站页面
│   ├── views/HomeView.vue        # 首页
│   ├── views/DocView.vue         # 文档页
│   ├── docs/                     # 文档注册表、Markdown 渲染、导航
│   └── styles/main.css           # 设计令牌与全局样式
├── guide/                        # 文档正文（Markdown）
├── schema/software.schema.json   # 下载数据 JSON Schema
├── docs/DATA_FORMAT.md           # 下载数据格式说明
├── scripts/
│   ├── check_links.py            # 下载链接可达性检查
│   ├── fix_links.py              # 一次性链接修复脚本（历史记录）
│   └── postbuild.mjs             # sitemap.xml / 404.html 生成
├── public/                       # logo、robots.txt
└── deploy.ps1                    # Cloudflare Pages 部署
```

## 部署

```bash
npm run deploy
```

需要已登录 wrangler（未登录会自动打开浏览器），或设置 `CLOUDFLARE_API_TOKEN`（建议同时设置 `CLOUDFLARE_ACCOUNT_ID`）。Cloudflare Pages 项目名为 `devbox-docs`，自定义域为 `devenv.cn`。

CI 在推送 `master` / `main` 且改动 `website/**` 时自动构建并部署，见 `.github/workflows/docs.yml`。
