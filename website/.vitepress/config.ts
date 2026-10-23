import { defineConfig } from 'vitepress'

// Cloudflare Pages：https://docs.devenv.cn/
const repo = 'pengcunfu/DevBox.Net'
const defaultBranch = 'master'

export default defineConfig({
  title: 'DevEnv',
  description: '程序盒子（DevEnv）：Windows 开发环境管理工具（开源 MIT）· 含熔岩绿色版环境管理',
  lang: 'zh-CN',
  // Pages 根路径部署（非 GitHub Pages 子路径）
  base: '/',
  outDir: 'dist',
  cleanUrls: true,
  lastUpdated: true,
  themeConfig: {
    logo: '/logo.svg',
    nav: [
      { text: '指南', link: '/guide/getting-started' },
      { text: '功能', link: '/guide/features' },
      { text: '熔岩环境', link: '/guide/lava/getting-started' },
      { text: '熔岩翻译', link: '/guide/translator/getting-started' },
      { text: '网络监控', link: '/guide/networkwatch/getting-started' },
      { text: '进程监控', link: '/guide/processwatch/getting-started' },
      { text: '熔岩剪贴板', link: '/guide/clipboard/getting-started' },
      { text: '下载', link: '/guide/releases' },
      {
        text: 'GitHub',
        link: `https://github.com/${repo}`,
      },
    ],
    sidebar: [
      {
        text: '介绍',
        items: [
          { text: '什么是 DevEnv', link: '/' },
          { text: '快速开始', link: '/guide/getting-started' },
          { text: '开源协议', link: '/guide/license' },
        ],
      },
      {
        text: '使用',
        items: [
          { text: '功能概览', link: '/guide/features' },
          { text: '配置说明', link: '/guide/configuration' },
        ],
      },
      {
        text: '熔岩环境',
        items: [
          { text: '快速开始', link: '/guide/lava/getting-started' },
          { text: '进程管理', link: '/guide/lava/processes' },
          { text: '下载与环境', link: '/guide/lava/download' },
          { text: '工具菜单', link: '/guide/lava/tools' },
          { text: '数据目录', link: '/guide/lava/data-directory' },
          { text: '常见问题', link: '/guide/lava/faq' },
        ],
      },
      {
        text: '熔岩翻译',
        items: [
          { text: '快速开始', link: '/guide/translator/getting-started' },
        ],
      },
      {
        text: '网络监控',
        items: [
          { text: '快速开始', link: '/guide/networkwatch/getting-started' },
        ],
      },
      {
        text: '进程监控',
        items: [
          { text: '快速开始', link: '/guide/processwatch/getting-started' },
        ],
      },
      {
        text: '熔岩剪贴板',
        items: [
          { text: '快速开始', link: '/guide/clipboard/getting-started' },
        ],
      },
      {
        text: '开发',
        items: [
          { text: '从源码构建', link: '/guide/build' },
          { text: '版本发布', link: '/guide/releases' },
        ],
      },
    ],
    socialLinks: [
      {
        icon: 'github',
        link: `https://github.com/${repo}`,
      },
    ],
    footer: {
      message: 'Released under the MIT License.',
      copyright: 'Copyright © pengcunfu',
    },
    editLink: {
      pattern: `https://github.com/${repo}/edit/${defaultBranch}/website/:path`,
      text: '在 GitHub 上编辑此页',
    },
  },
})
