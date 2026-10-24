// 构建后处理：生成 sitemap.xml、Cloudflare Pages 使用的 404.html 与跳转规则。
import { execFileSync } from 'node:child_process'
import { readdirSync, statSync, writeFileSync } from 'node:fs'
import { join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = fileURLToPath(new URL('..', import.meta.url))
const dist = join(root, 'dist')
const siteUrl = 'https://docs.devenv.cn'

function walk(directory) {
  return readdirSync(directory).flatMap((name) => {
    const full = join(directory, name)
    return statSync(full).isDirectory() ? walk(full) : [full]
  })
}

const pages = walk(dist)
  .filter((file) => file.endsWith('.html') && !file.endsWith('404.html'))
  .map((file) => `/${relative(dist, file).split('\\').join('/')}`)
  .map((path) => path.replace(/\/index\.html$/, '/').replace(/\.html$/, ''))
  .map((path) => (path === '' ? '/' : path))

const urls = [...new Set(['/', ...pages])].sort()
const buildDate = new Date().toISOString().slice(0, 10)

/** 页面 URL 对应的源文件（用于取 git 最后提交日期）。 */
function sourceFileFor(url) {
  if (url === '/') return 'src/views/HomeView.vue'
  if (url === '/downloads' || url.startsWith('/downloads/')) return 'src/data/software.json'
  return `${url.slice(1)}.md`
}

/**
 * 用 git 最后提交日期作为 lastmod；取不到时回退到构建日期。
 * 统一用构建日期会让所有 URL 的 lastmod 相同，搜索引擎会忽略这个信号。
 */
function lastmodFor(url) {
  try {
    const date = execFileSync('git', ['log', '-1', '--format=%cs', '--', sourceFileFor(url)], {
      cwd: root,
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'ignore'],
    }).trim()
    return /^\d{4}-\d{2}-\d{2}$/.test(date) ? date : buildDate
  } catch {
    return buildDate
  }
}

const sitemap = `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${urls.map((url) => `  <url><loc>${siteUrl}${url}</loc><lastmod>${lastmodFor(url)}</lastmod></url>`).join('\n')}
</urlset>
`

writeFileSync(join(dist, 'sitemap.xml'), sitemap, 'utf8')

// Cloudflare Pages 会同时响应 /guide/foo、/guide/foo.html 与 /guide/foo/，
// 后两者统一 301 到规范地址，避免重复内容分散权重。
const cleanPages = pages.filter((path) => path !== '/' && !path.endsWith('/'))
const redirects = [
  '/index.html / 301',
  ...cleanPages.map((path) => `${path}.html ${path} 301`),
  ...cleanPages.map((path) => `${path}/ ${path} 301`),
]
writeFileSync(join(dist, '_redirects'), `${redirects.join('\n')}\n`, 'utf8')

const notFound = `<!doctype html>
<html lang="zh-CN">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <meta name="robots" content="noindex" />
    <title>页面不存在 · DevEnv</title>
    <style>
      :root { color-scheme: light dark; }
      body {
        display: flex; flex-direction: column; align-items: center; justify-content: center;
        min-height: 100vh; margin: 0; padding: 24px; text-align: center;
        background: #f6f8fb; color: #0f172a;
        font-family: 'Segoe UI', 'Microsoft YaHei UI', system-ui, sans-serif;
      }
      .code { margin: 0; font-family: Consolas, monospace; font-size: 96px; font-weight: 800;
        line-height: 1; letter-spacing: -0.06em; color: #0d9488; }
      h1 { margin: 16px 0 10px; font-size: 22px; }
      p { margin: 0 0 26px; color: #667085; font-size: 14px; }
      .actions { display: flex; gap: 12px; flex-wrap: wrap; justify-content: center; }
      a { display: inline-flex; align-items: center; height: 42px; padding: 0 20px;
        border-radius: 10px; border: 1px solid #e3e8ef; background: #fff;
        color: #0f172a; font-size: 14px; font-weight: 600; text-decoration: none; }
      a.primary { border-color: transparent; background: #0d9488; color: #fff; }
      @media (prefers-color-scheme: dark) {
        body { background: #070d19; color: #e6edf7; }
        p { color: #8b9bb0; }
        a { border-color: #1e2a3f; background: #0d1526; color: #e6edf7; }
        a.primary { border-color: transparent; background: #2dd4bf; color: #04211d; }
      }
    </style>
  </head>
  <body>
    <p class="code">404</p>
    <h1>页面不存在</h1>
    <p>你访问的地址可能已经移动，或者链接拼写有误。</p>
    <div class="actions">
      <a class="primary" href="/">回到首页</a>
      <a href="/guide/getting-started">阅读文档</a>
    </div>
  </body>
</html>
`

writeFileSync(join(dist, '404.html'), notFound, 'utf8')

console.log(
  `[postbuild] sitemap.xml: ${urls.length} urls, _redirects: ${redirects.length} rules, 404.html written`,
)
