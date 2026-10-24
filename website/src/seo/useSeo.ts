import { useHead } from '@unhead/vue'
import { computed, toValue, type MaybeRefOrGetter } from 'vue'
import { siteUrl } from '@/docs/nav'

export const siteName = 'DevEnv · 燧岚开发盒子'

/**
 * 社交平台大多不识别 SVG，条件允许时把这里换成 1200×630 的 PNG（放到 public/ 下）。
 */
export const defaultOgImage = `${siteUrl}/logo.svg`

export type SeoType = 'website' | 'article'

export interface SeoOptions {
  title: MaybeRefOrGetter<string>
  description: MaybeRefOrGetter<string>
  path: MaybeRefOrGetter<string>
  type?: MaybeRefOrGetter<SeoType>
  image?: MaybeRefOrGetter<string>
  /** 文档分组，写入 article:section。 */
  section?: MaybeRefOrGetter<string | undefined>
  jsonLd?: MaybeRefOrGetter<Record<string, unknown> | Record<string, unknown>[] | undefined>
  robots?: MaybeRefOrGetter<string | undefined>
}

/** 默认允许搜索引擎展示大图预览与完整摘要。 */
const defaultRobots = 'index, follow, max-image-preview:large, max-snippet:-1, max-video-preview:-1'

/** 把站内路径转成带域名的绝对地址（canonical / og:url 必须是绝对地址）。 */
export function absoluteUrl(path: string): string {
  if (path === '/') return `${siteUrl}/`
  return `${siteUrl}${path.startsWith('/') ? path : `/${path}`}`
}

/**
 * 每个页面统一注入 title / description / canonical / Open Graph / Twitter / JSON-LD。
 *
 * vite-ssg 会在构建时执行这些 head 配置，因此每份 dist/*.html 都会带上
 * 对应页面自己的元信息，而不是共用首页的默认值。
 */
export function useSeo(options: SeoOptions): void {
  useHead(
    computed(() => {
      const title = toValue(options.title)
      const description = toValue(options.description)
      const path = toValue(options.path)
      const type = toValue(options.type) ?? 'website'
      const image = toValue(options.image) ?? defaultOgImage
      const section = toValue(options.section)
      const jsonLd = toValue(options.jsonLd)
      const robots = toValue(options.robots) ?? defaultRobots
      const canonical = absoluteUrl(path)

      const meta: Record<string, string>[] = [
        { name: 'description', content: description },
        { property: 'og:type', content: type },
        { property: 'og:site_name', content: siteName },
        { property: 'og:title', content: title },
        { property: 'og:description', content: description },
        { property: 'og:url', content: canonical },
        { property: 'og:image', content: image },
        { property: 'og:image:alt', content: title },
        { property: 'og:locale', content: 'zh_CN' },
        { name: 'twitter:card', content: 'summary_large_image' },
        { name: 'twitter:title', content: title },
        { name: 'twitter:description', content: description },
        { name: 'twitter:image', content: image },
        { name: 'robots', content: robots },
      ]

      if (type === 'article' && section) {
        meta.push({ property: 'article:section', content: section })
      }

      return {
        title,
        meta,
        link: [{ rel: 'canonical', href: canonical }],
        script: jsonLd
          ? [
              {
                id: 'seo-jsonld',
                type: 'application/ld+json',
                innerHTML: JSON.stringify(jsonLd),
              },
            ]
          : [],
      }
    }),
  )
}
