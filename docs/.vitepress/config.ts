import { defineConfig } from 'vitepress'

// GitHub Pages 项目站点：https://pengcunfu.github.io/DevBox.Net/
const repoName = process.env.GITHUB_REPOSITORY?.split('/')[1] ?? 'DevBox.Net'
const repo = 'pengcunfu/DevBox.Net'
const defaultBranch = 'master'

export default defineConfig({
  title: 'DevBox',
  description: 'Windows 开发环境管理工具（开源 MIT）',
  lang: 'zh-CN',
  base: `/${repoName}/`,
  cleanUrls: true,
  lastUpdated: true,
  themeConfig: {
    logo: '/logo.svg',
    nav: [
      { text: '指南', link: '/guide/getting-started' },
      { text: '功能', link: '/guide/features' },
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
          { text: '什么是 DevBox', link: '/' },
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
      pattern: `https://github.com/${repo}/edit/${defaultBranch}/docs/:path`,
      text: '在 GitHub 上编辑此页',
    },
  },
})
