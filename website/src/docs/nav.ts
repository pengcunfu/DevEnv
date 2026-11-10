export interface SidebarItem {
  text: string
  path: string
  /** 页面 meta description；缺省时由正文首段自动生成。 */
  description?: string
}

export interface SidebarGroup {
  text: string
  items: SidebarItem[]
}

export const repo = 'pengcunfu/DevEnv'
export const repoUrl = `https://github.com/${repo}`
export const releasesUrl = `${repoUrl}/releases/latest`
export const issuesUrl = `${repoUrl}/issues`
export const siteUrl = 'https://devenv.cn'
export const editBase = `${repoUrl}/edit/master/website/`

/** 友链：品牌站与作者站点。 */
export const friendLinks = [
  { text: 'FireNeb', url: 'https://fireneb.com' },
  { text: 'Sigxor', url: 'https://sigxor.cn' },
]

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
      {
        text: '快速开始',
        path: '/guide/getting-started',
        description:
          '从 GitHub Releases 下载 FireNeb DevEnv Windows 安装包，完成首次启动与数据目录初始化，并了解应用内自动更新。',
      },
      {
        text: '开源协议',
        path: '/guide/license',
        description:
          'FireNeb DevEnv 采用 Apache License 2.0 开源，可自由使用、修改与分发；了解版权与归属声明、修改标注、专利授权与免责声明。',
      },
    ],
  },
  {
    text: '使用',
    items: [
      {
        text: '功能概览',
        path: '/guide/features',
        description:
          'DevEnv 功能概览：多语言开发环境与版本切换、Windows 系统管理工具、熔岩环境、网页翻译、网络与进程监控、剪贴板历史。',
      },
      {
        text: '配置说明',
        path: '/guide/configuration',
        description:
          'DevEnv 配置说明：认识文档目录下的 data.json、图标与熔岩配置文件，了解偏好设置、备份迁移与手动修改注意事项。',
      },
      {
        text: '文件解锁与粉碎',
        path: '/guide/filetools/getting-started',
        description:
          '使用 DevEnv 的文件解锁与粉碎工具：通过 Restart Manager 定位占用文件的进程，并用随机数据多遍覆盖后删除文件或文件夹。',
      },
    ],
  },
  {
    text: '熔岩环境',
    items: [
      {
        text: '快速开始',
        path: '/guide/lava/getting-started',
        description:
          '熔岩环境是 DevEnv 内嵌的绿色版开发环境管理模块，可在 Windows 上一键下载、配置与管理 MySQL、Redis、Nginx 等本地服务。',
      },
      {
        text: '进程管理',
        path: '/guide/lava/processes',
        description:
          '了解熔岩环境如何以独立进程运行 MySQL、PostgreSQL、Redis、MongoDB、MinIO、Nginx、Kafka 等绿色版服务，以及各状态的含义。',
      },
      {
        text: '下载与环境',
        path: '/guide/lava/download',
        description:
          '在熔岩环境中下载并自动解压数据库、缓存、Web 服务器等绿色版软件，管理下载任务，并使用环境扫描与镜像配置。',
      },
      {
        text: '小工具',
        path: '/guide/lava/tools',
        description:
          '熔岩环境工具栏提供环境变量、应用目录、数据目录等快捷入口；JSON、Hosts、图像转换与文件哈希等工具已归入启动器左侧「内置工具」页签。',
      },
      {
        text: '数据目录',
        path: '/guide/lava/data-directory',
        description:
          '熔岩环境的软件包、服务与配置统一存放在 文档\\FNSoftware\\DevEnv 数据目录；了解目录结构、默认路径与迁移方式。',
      },
      {
        text: '常见问题',
        path: '/guide/lava/faq',
        description:
          '熔岩环境常见问题：找不到入口、Hosts 编辑与生效、服务未安装、端口冲突、旧版独立安装迁移与问题反馈方式。',
      },
    ],
  },
  {
    text: '熔岩翻译',
    items: [
      {
        text: '快速开始',
        path: '/guide/translator/getting-started',
        description:
          '熔岩翻译是 DevEnv 内嵌的网页翻译助手，在 WebView2 中打开有道、搜狗、百度、谷歌、DeepL、必应、腾讯等站点，并用快捷键填入剪贴板文本。',
      },
    ],
  },
  {
    text: '网络监控',
    items: [
      {
        text: '快速开始',
        path: '/guide/networkwatch/getting-started',
        description:
          '网络监控内嵌于 DevEnv，按进程展示 TCP/UDP 连接与上下行速率，支持连接详情、搜索、按流量排序以及延迟、下载与上传测速。',
      },
    ],
  },
  {
    text: '进程监控',
    items: [
      {
        text: '快速开始',
        path: '/guide/processwatch/getting-started',
        description:
          '进程监控内嵌于 DevEnv，实时查看进程 CPU、内存、线程数与可执行路径，支持搜索排序、进程详情与结束进程等操作。',
      },
    ],
  },
  {
    text: '服务监控',
    items: [
      {
        text: '快速开始',
        path: '/guide/servicewatch/getting-started',
        description:
          '服务监控内嵌于 DevEnv，替代 Windows 服务面板，查看服务状态、启动类型与依赖关系，并支持启停、重启与暂停。',
      },
    ],
  },
  {
    text: '熔岩剪贴板',
    items: [
      {
        text: '快速开始',
        path: '/guide/clipboard/getting-started',
        description:
          '熔岩剪贴板自动记录文本与图片剪贴板历史，支持按时间与类型筛选、预览、复制、清空导出，以及全局热键快速唤起。',
      },
    ],
  },
  {
    text: '开发',
    items: [
      {
        text: '从源码构建',
        path: '/guide/build',
        description:
          '从源码构建 DevEnv：准备 .NET 10 SDK 与 Git，克隆仓库、恢复依赖、编译运行 WPF 主程序，并了解各项目输出目录与清理方式。',
      },
      {
        text: '版本发布',
        path: '/guide/releases',
        description:
          'DevEnv 通过 GitHub Releases 以便携 zip 与自研自更新分发 Windows 64 位版本；了解首次安装、可选组件、自动更新与版本发布流程。',
      },
    ],
  },
]

export const sidebarOrder: SidebarItem[] = sidebarGroups.flatMap((group) => group.items)
