import { sidebarGroups, sidebarOrder } from './nav'
import { slugify } from './slug'

export interface DocEntry {
  /** 站点路由，例如 /guide/lava/faq */
  path: string
  /** 仓库内路径，例如 guide/lava/faq.md */
  file: string
  /** 文档所在目录（用于解析相对链接），例如 guide/lava */
  dir: string
  title: string
  description: string
  group: string
  raw: string
}

export interface SearchSection {
  docPath: string
  docTitle: string
  group: string
  heading: string
  id: string
  text: string
}

const rawDocs = import.meta.glob('../../guide/**/*.md', {
  query: '?raw',
  import: 'default',
  eager: true,
}) as Record<string, string>

const groupByPath = new Map<string, string>()
const descriptionByPath = new Map<string, string>()
for (const group of sidebarGroups) {
  for (const item of group.items) {
    groupByPath.set(item.path, group.text)
    if (item.description) descriptionByPath.set(item.path, item.description)
  }
}

const orderByPath = new Map<string, number>()
sidebarOrder.forEach((item, index) => orderByPath.set(item.path, index))

function extractTitle(raw: string): string {
  const match = raw.match(/^#\s+(.+?)\s*$/m)
  return match ? match[1].replace(/[`*_]/g, '') : '文档'
}

function extractDescription(raw: string): string {
  let afterTitle = false
  for (const line of raw.split(/\r?\n/)) {
    const text = line.trim()
    if (!afterTitle) {
      if (text.startsWith('# ')) afterTitle = true
      continue
    }
    if (!text || text.startsWith('#') || text.startsWith(':::') || text.startsWith('|')) continue
    if (text.startsWith('```') || text.startsWith('![') || text.startsWith('>')) continue
    // 跳过列表项，优先取正文段落作为页面描述。
    if (/^([-*+]|\d+[.)])\s/.test(text)) continue
    return text
      .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')
      .replace(/<[^>]+>/g, '')
      .replace(/[`*_]/g, '')
      .slice(0, 120)
  }
  return ''
}

function toPlainText(text: string): string {
  return text
    .replace(/<[^>]+>/g, ' ')
    .replace(/!\[[^\]]*\]\([^)]*\)/g, ' ')
    .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/:::[^\n]*/g, ' ')
    .replace(/[`*_~|>#]/g, '')
    .replace(/\s+/g, ' ')
    .trim()
}

function buildSections(doc: DocEntry): SearchSection[] {
  const sections: SearchSection[] = []
  let heading = doc.title
  let id = ''
  let buffer: string[] = []
  let inFence = false

  const flush = () => {
    const text = toPlainText(buffer.join(' '))
    if (text) {
      sections.push({
        docPath: doc.path,
        docTitle: doc.title,
        group: doc.group,
        heading,
        id,
        text,
      })
    }
    buffer = []
  }

  for (const line of doc.raw.split(/\r?\n/)) {
    if (/^\s*```/.test(line)) {
      inFence = !inFence
      buffer.push(line)
      continue
    }
    if (!inFence) {
      const match = line.match(/^(#{2,3})\s+(.+?)\s*$/)
      if (match) {
        flush()
        heading = toPlainText(match[2])
        id = slugify(match[2])
        continue
      }
    }
    buffer.push(line)
  }
  flush()

  return sections
}

const entries: DocEntry[] = Object.entries(rawDocs).map(([key, raw]) => {
  const file = key.slice(key.indexOf('guide/'))
  const withoutExt = file.replace(/\.md$/i, '')
  const path = `/${withoutExt}`
  const dir = withoutExt.includes('/') ? withoutExt.slice(0, withoutExt.lastIndexOf('/')) : '.'

  return {
    path,
    file,
    dir,
    title: extractTitle(raw),
    description: descriptionByPath.get(path) || extractDescription(raw),
    group: groupByPath.get(path) || '文档',
    raw,
  }
})

entries.sort((a, b) => {
  const orderA = orderByPath.get(a.path) ?? Number.MAX_SAFE_INTEGER
  const orderB = orderByPath.get(b.path) ?? Number.MAX_SAFE_INTEGER
  if (orderA !== orderB) return orderA - orderB
  return a.title.localeCompare(b.title, 'zh-CN')
})

export const docs = entries
export const docsByPath = new Map(entries.map((doc) => [doc.path, doc]))

export const searchSections: SearchSection[] = entries.flatMap(buildSections)

export function getDoc(path: string): DocEntry | undefined {
  return docsByPath.get(path)
}

/** 侧边栏分组下的第一篇文档，用作该分组的落地页（面包屑结构化数据）。 */
export function getGroupEntryPath(group: string): string | undefined {
  return sidebarGroups.find((item) => item.text === group)?.items[0]?.path
}

export function getPrevNext(path: string): { prev?: DocEntry; next?: DocEntry } {
  const index = entries.findIndex((doc) => doc.path === path)
  if (index < 0) return {}
  return {
    prev: index > 0 ? entries[index - 1] : undefined,
    next: index < entries.length - 1 ? entries[index + 1] : undefined,
  }
}
