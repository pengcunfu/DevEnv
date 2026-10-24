import rawData from '@/data/software.json'

export interface SoftwareVersion {
  version: string
  url: string
  file_name: string
  source?: string
  size?: string
  os?: 'windows' | 'macos' | 'linux'
  arch?: 'x64' | 'arm64'
  remark?: string
}

export interface DownloadSoftware {
  name: string
  desc?: string
  icon?: string
  official_site?: string
  default_source?: string
  versions: SoftwareVersion[]
}

export interface DownloadCategory {
  name: string
  id?: string
  icon?: string
  softwares: DownloadSoftware[]
}

export interface DownloadData {
  sitename: string
  description?: string
  categories: DownloadCategory[]
}

export const downloadData = rawData as DownloadData
export const downloadCategories = downloadData.categories

export const downloadStats = {
  categories: downloadCategories.length,
  softwares: downloadCategories.reduce((total, category) => total + category.softwares.length, 0),
  versions: downloadCategories.reduce(
    (total, category) =>
      total +
      category.softwares.reduce((count, software) => count + (software.versions?.length || 0), 0),
    0,
  ),
}

/**
 * 分类名 → URL slug（如 Node.js → node-js）。
 *
 * 优先使用数据里的 ASCII id：中文放进 URL 后，不同浏览器的编码 / 解码
 * 方式不一致，会导致分类切换失效。没有显式 id 时回退到名称 slug。
 */
export function categorySlug(category: Pick<DownloadCategory, 'name' | 'id'>): string {
  if (category.id) return category.id
  return category.name
    .toLowerCase()
    .replace(/[^a-z0-9一-龥]+/g, '-')
    .replace(/^-+|-+$/g, '')
}

/** 分类锚点 id（如 cat-node-js），保留给旧的 /downloads#cat-xxx 深链。 */
export function categoryAnchor(category: Pick<DownloadCategory, 'name' | 'id'>): string {
  return `cat-${categorySlug(category)}`
}

/** 分类独立落地页路径（如 /downloads/node-js）。 */
export function categoryPagePath(category: Pick<DownloadCategory, 'name' | 'id'>): string {
  return `/downloads/${categorySlug(category)}`
}
