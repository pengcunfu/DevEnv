import { releasesUrl, repoUrl, siteUrl } from '@/docs/nav'
import { absoluteUrl, siteName } from './useSeo'

const publisher = {
  '@type': 'Organization',
  name: '程序盒子',
  url: `${siteUrl}/`,
  logo: `${siteUrl}/logo.svg`,
  sameAs: [repoUrl],
}

export function websiteSchema(): Record<string, unknown> {
  return {
    '@context': 'https://schema.org',
    '@type': 'WebSite',
    name: siteName,
    alternateName: 'DevEnv',
    url: `${siteUrl}/`,
    inLanguage: 'zh-CN',
    publisher,
  }
}

export function softwareSchema(version: string, description: string): Record<string, unknown> {
  return {
    '@context': 'https://schema.org',
    '@type': 'SoftwareApplication',
    name: '程序盒子（DevEnv）',
    alternateName: 'DevEnv',
    applicationCategory: 'DeveloperApplication',
    operatingSystem: 'Windows 10, Windows 11',
    softwareVersion: version,
    description,
    url: `${siteUrl}/`,
    downloadUrl: releasesUrl,
    license: 'https://www.apache.org/licenses/LICENSE-2.0',
    inLanguage: 'zh-CN',
    offers: {
      '@type': 'Offer',
      price: '0',
      priceCurrency: 'CNY',
    },
    publisher,
    sameAs: [repoUrl],
  }
}

export function sourceCodeSchema(version: string, description: string): Record<string, unknown> {
  return {
    '@context': 'https://schema.org',
    '@type': 'SoftwareSourceCode',
    name: '程序盒子（DevEnv）',
    description,
    codeRepository: repoUrl,
    programmingLanguage: 'C#',
    runtimePlatform: 'Windows',
    license: 'https://www.apache.org/licenses/LICENSE-2.0',
    version,
    inLanguage: 'zh-CN',
    author: {
      '@type': 'Person',
      name: 'pengcunfu',
      url: 'https://github.com/pengcunfu',
    },
  }
}

export interface BreadcrumbItem {
  name: string
  path?: string
}

export function breadcrumbSchema(items: BreadcrumbItem[]): Record<string, unknown> {
  return {
    '@context': 'https://schema.org',
    '@type': 'BreadcrumbList',
    itemListElement: items.map((item, index) => ({
      '@type': 'ListItem',
      position: index + 1,
      name: item.name,
      ...(item.path ? { item: absoluteUrl(item.path) } : {}),
    })),
  }
}

export function articleSchema(input: {
  title: string
  description: string
  path: string
  group: string
}): Record<string, unknown> {
  const url = absoluteUrl(input.path)
  return {
    '@context': 'https://schema.org',
    '@type': 'TechArticle',
    headline: input.title,
    description: input.description,
    url,
    mainEntityOfPage: url,
    inLanguage: 'zh-CN',
    articleSection: input.group,
    isPartOf: {
      '@type': 'WebSite',
      name: `${siteName} 文档`,
      url: `${siteUrl}/`,
    },
    publisher,
  }
}

export function collectionSchema(input: {
  name: string
  description: string
  path: string
  items?: { name: string; url: string }[]
}): Record<string, unknown> {
  return {
    '@context': 'https://schema.org',
    '@type': 'CollectionPage',
    name: input.name,
    description: input.description,
    url: absoluteUrl(input.path),
    inLanguage: 'zh-CN',
    isPartOf: {
      '@type': 'WebSite',
      name: siteName,
      url: `${siteUrl}/`,
    },
    publisher,
    ...(input.items?.length
      ? {
          mainEntity: {
            '@type': 'ItemList',
            numberOfItems: input.items.length,
            itemListElement: input.items.map((item, index) => ({
              '@type': 'ListItem',
              position: index + 1,
              name: item.name,
              url: item.url,
            })),
          },
        }
      : {}),
  }
}

function stripMarkdown(text: string): string {
  return text
    // 保留代码块内容（FAQ 里常是操作命令），只去掉围栏与语言标记。
    .replace(/```[^\s`]*/g, ' ')
    .replace(/`([^`]*)`/g, '$1')
    .replace(/!\[[^\]]*\]\([^)]*\)/g, ' ')
    .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/:::[^\n]*/g, ' ')
    .replace(/<[^>]+>/g, ' ')
    .replace(/[*_~`>|#]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
}

/** 从 Markdown 的 h2 段落生成 FAQPage 结构化数据（用于常见问题页）。 */
export function faqSchema(raw: string, path: string): Record<string, unknown> | undefined {
  const items: { question: string; answer: string }[] = []
  let question = ''
  let answerLines: string[] = []

  const flush = () => {
    const answer = stripMarkdown(answerLines.join(' '))
    // 「文档与反馈」这类只有链接列表的小节不是问答，跳过。
    const linkOnly = answerLines
      .filter((line) => line.trim())
      .every((line) =>
        /^[-*+]\s+.*(?:\[[^\]]*\]\([^)]*\)|<https?:\/\/[^>]+>)\s*$/.test(line.trim()),
      )
    if (question && answer && !linkOnly) items.push({ question, answer })
    answerLines = []
  }

  for (const line of raw.split(/\r?\n/)) {
    if (/^\s*```/.test(line)) {
      answerLines.push(line)
      continue
    }
    const heading = line.match(/^##\s+(.+?)\s*$/)
    if (heading) {
      flush()
      question = stripMarkdown(heading[1])
      continue
    }
    if (/^#\s+/.test(line)) continue
    answerLines.push(line)
  }
  flush()

  if (!items.length) return undefined

  return {
    '@context': 'https://schema.org',
    '@type': 'FAQPage',
    url: absoluteUrl(path),
    inLanguage: 'zh-CN',
    mainEntity: items.map((item) => ({
      '@type': 'Question',
      name: item.question,
      acceptedAnswer: {
        '@type': 'Answer',
        text: item.answer,
      },
    })),
  }
}
