import { defineConfig } from 'vitepress'

export default defineConfig({
  title: '熔岩环境管理工具',
  description: '熔岩环境管理工具帮助文档',
  lang: 'zh-CN',
  base: '/devenv/',
  themeConfig: {
    nav: [
      { text: '指南', link: '/guide/getting-started' },
      { text: '常见问题', link: '/guide/faq' },
      { text: 'GitHub', link: 'https://github.com/pengcunfu/devenv' },
    ],
    sidebar: [
      {
        text: '使用指南',
        items: [
          { text: '快速开始', link: '/guide/getting-started' },
          { text: '进程管理', link: '/guide/processes' },
          { text: '下载与环境', link: '/guide/download' },
          { text: '小工具', link: '/guide/tools' },
          { text: '数据目录', link: '/guide/data-directory' },
          { text: '常见问题', link: '/guide/faq' },
        ],
      },
    ],
    socialLinks: [{ icon: 'github', link: 'https://github.com/pengcunfu/devenv' }],
    footer: {
      message: 'MIT License',
      copyright: 'Copyright © 熔岩环境管理工具 Contributors',
    },
  },
})
