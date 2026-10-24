import hljs from 'highlight.js/lib/core'
import bash from 'highlight.js/lib/languages/bash'
import csharp from 'highlight.js/lib/languages/csharp'
import dos from 'highlight.js/lib/languages/dos'
import ini from 'highlight.js/lib/languages/ini'
import javascript from 'highlight.js/lib/languages/javascript'
import json from 'highlight.js/lib/languages/json'
import plaintext from 'highlight.js/lib/languages/plaintext'
import powershell from 'highlight.js/lib/languages/powershell'
import typescript from 'highlight.js/lib/languages/typescript'
import xml from 'highlight.js/lib/languages/xml'
import yaml from 'highlight.js/lib/languages/yaml'
import MarkdownIt from 'markdown-it'
import anchor from 'markdown-it-anchor'
import container from 'markdown-it-container'
import { slugify } from './slug'

hljs.registerLanguage('bash', bash)
hljs.registerLanguage('csharp', csharp)
hljs.registerLanguage('dos', dos)
hljs.registerLanguage('ini', ini)
hljs.registerLanguage('javascript', javascript)
hljs.registerLanguage('json', json)
hljs.registerLanguage('plaintext', plaintext)
hljs.registerLanguage('powershell', powershell)
hljs.registerLanguage('typescript', typescript)
hljs.registerLanguage('xml', xml)
hljs.registerLanguage('yaml', yaml)

const langAliases: Record<string, string> = {
  bat: 'dos',
  cmd: 'dos',
  ps1: 'powershell',
  pwsh: 'powershell',
  sh: 'bash',
  shell: 'bash',
  jsonc: 'json',
  yml: 'yaml',
}

const blockTitles: Record<string, string> = {
  tip: '提示',
  info: '说明',
  warning: '注意',
  danger: '警告',
}

export interface TocItem {
  id: string
  text: string
  level: number
}

export interface RenderedDoc {
  html: string
  toc: TocItem[]
}

interface RenderEnv {
  docDir?: string
}

function resolveDocPath(dir: string, relative: string): string {
  const stack: string[] = []
  for (const part of `${dir}/${relative}`.split('/')) {
    if (!part || part === '.') continue
    if (part === '..') stack.pop()
    else stack.push(part)
  }
  return stack.join('/').replace(/\.md$/i, '')
}

const md = new MarkdownIt({
  html: true,
  linkify: true,
  typographer: false,
})

md.use(anchor, {
  slugify,
  level: [1, 2, 3, 4],
  // 用 CSS 伪元素显示 #，避免标题正文里混入锚点字符（利于标题文本提取）。
  permalink: anchor.permalink.linkInsideHeader({
    symbol: '',
    placement: 'before',
    class: 'header-anchor',
    ariaHidden: true,
  }),
})

for (const type of Object.keys(blockTitles)) {
  md.use(container, type, {
    render(tokens, idx) {
      const token = tokens[idx]
      if (token.nesting === 1) {
        const info = token.info
          .trim()
          .slice(type.length)
          .trim()
        const title = info ? md.utils.escapeHtml(info) : blockTitles[type]
        return `<div class="custom-block ${type}"><p class="custom-block-title">${title}</p>\n`
      }
      return '</div>\n'
    },
  })
}

// 相对 .md 链接按当前文档目录解析成站点路由；外链补 target / rel。
md.core.ruler.push('devenv-links', (state) => {
  const env = state.env as RenderEnv
  const docDir = env?.docDir || 'guide'

  for (const token of state.tokens) {
    if (token.type !== 'inline' || !token.children) continue
    for (const child of token.children) {
      if (child.type !== 'link_open') continue
      const href = child.attrGet('href')
      if (!href) continue

      if (/^https?:\/\//i.test(href)) {
        child.attrSet('target', '_blank')
        child.attrSet('rel', 'noopener noreferrer')
        continue
      }
      if (/^(?:[a-z][a-z0-9+.-]*:|\/|#)/i.test(href)) continue

      const [pathPart, hash] = href.split('#')
      if (!/\.md$/i.test(pathPart)) continue

      const resolved = resolveDocPath(docDir, pathPart)
      child.attrSet('href', `/${resolved}${hash ? `#${hash}` : ''}`)
    }
  }
})

// 代码块：高亮 + 语言标签 + 复制按钮（点击在 DocView 里统一委托）。
md.renderer.rules.fence = (tokens, idx) => {
  const token = tokens[idx]
  const info = token.info.trim().split(/\s+/)[0] || ''
  const normalized = info.toLowerCase()
  const language = langAliases[normalized] || normalized
  const canHighlight = language !== '' && hljs.getLanguage(language) !== undefined
  const code = canHighlight
    ? hljs.highlight(token.content, { language, ignoreIllegals: true }).value
    : md.utils.escapeHtml(token.content)
  const label = md.utils.escapeHtml(info || 'text')
  const languageClass = canHighlight ? ` language-${language}` : ''

  return (
    `<div class="code-block" data-lang="${label}">` +
    `<div class="code-head"><span class="code-lang">${label}</span>` +
    `<button type="button" class="code-copy" aria-label="复制代码">复制</button></div>` +
    `<pre class="code-pre"><code class="hljs${languageClass}">${code}</code></pre>` +
    `</div>\n`
  )
}

export function renderDoc(raw: string, docDir: string): RenderedDoc {
  const env: RenderEnv = { docDir }
  const tokens = md.parse(raw, env)
  const toc: TocItem[] = []

  tokens.forEach((token, index) => {
    if (token.type !== 'heading_open' || (token.tag !== 'h2' && token.tag !== 'h3')) return
    const id = token.attrGet('id')
    const inline = tokens[index + 1]
    if (!id) return
    toc.push({
      id,
      text: (inline?.content || '').replace(/[`*_]/g, ''),
      level: Number(token.tag.slice(1)),
    })
  })

  return {
    html: md.renderer.render(tokens, md.options, env),
    toc,
  }
}
