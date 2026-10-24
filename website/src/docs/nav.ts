export interface SidebarItem {
  text: string
  path: string
}

export interface SidebarGroup {
  text: string
  items: SidebarItem[]
}

export const repo = 'pengcunfu/DevBox.Net'
export const repoUrl = `https://github.com/${repo}`
export const releasesUrl = `${repoUrl}/releases/latest`
export const issuesUrl = `${repoUrl}/issues`
export const siteUrl = 'https://docs.devenv.cn'
export const editBase = `${repoUrl}/edit/master/website/`

export const primaryNav = [
  { text: '功能', path: '/guide/features' },
  { text: '文档', path: '/guide/getting-started' },
  { text: '熔岩环境', path: '/guide/lava/getting-started' },
  { text: '熔岩翻译', path: '/guide/translator/getting-started' },
  { text: '软件下载', path: '/downloads' },
  { text: '版本发布', path: '/guide/releases' },
]

export const sidebarGroups: SidebarGroup[] = [
  {
    text: '介绍',
    items: [
      { text: '快速开始', path: '/guide/getting-started' },
      { text: '开源协议', path: '/guide/license' },
    ],
  },
  {
    text: '使用',
    items: [
      { text: '功能概览', path: '/guide/features' },
      { text: '配置说明', path: '/guide/configuration' },
    ],
  },
  {
    text: '熔岩环境',
    items: [
      { text: '快速开始', path: '/guide/lava/getting-started' },
      { text: '进程管理', path: '/guide/lava/processes' },
      { text: '下载与环境', path: '/guide/lava/download' },
      { text: '小工具', path: '/guide/lava/tools' },
      { text: '数据目录', path: '/guide/lava/data-directory' },
      { text: '常见问题', path: '/guide/lava/faq' },
    ],
  },
  {
    text: '熔岩翻译',
    items: [{ text: '快速开始', path: '/guide/translator/getting-started' }],
  },
  {
    text: '网络监控',
    items: [{ text: '快速开始', path: '/guide/networkwatch/getting-started' }],
  },
  {
    text: '进程监控',
    items: [{ text: '快速开始', path: '/guide/processwatch/getting-started' }],
  },
  {
    text: '熔岩剪贴板',
    items: [{ text: '快速开始', path: '/guide/clipboard/getting-started' }],
  },
  {
    text: '开发',
    items: [
      { text: '从源码构建', path: '/guide/build' },
      { text: '版本发布', path: '/guide/releases' },
    ],
  },
]

export const sidebarOrder: SidebarItem[] = sidebarGroups.flatMap((group) => group.items)
