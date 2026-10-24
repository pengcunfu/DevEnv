<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import CategorySection from '@/components/downloads/CategorySection.vue'
import {
  categoryAnchor,
  categoryPagePath,
  downloadCategories,
  downloadData,
  downloadStats,
  type DownloadCategory,
} from '@/downloads/software'
import { editBase, issuesUrl, repoUrl } from '@/docs/nav'
import { breadcrumbSchema, collectionSchema } from '@/seo/schema'
import { absoluteUrl, useSeo } from '@/seo/useSeo'

const route = useRoute()
const query = ref('')
const contentRef = ref<HTMLElement>()

/** 分类独立页（/downloads/java）对应的分类；/downloads 本身为空。 */
const routeCategory = computed<DownloadCategory | undefined>(() => {
  const anchor = route.meta.category
  if (typeof anchor !== 'string') return undefined
  return downloadCategories.find((category) => categoryAnchor(category) === anchor)
})

const view = ref(routeCategory.value ? categoryAnchor(routeCategory.value) : 'all')

const keyword = computed(() => query.value.trim().toLowerCase())

const dataEditUrl = `${editBase}src/data/software.json`
const dataFormatUrl = `${repoUrl}/blob/master/website/docs/DATA_FORMAT.md`

const downloadDescription =
  'Windows 常用开发环境与工具安装包下载导航（国内镜像加速）：Java、Python、PHP、Node.js、Web 服务器、数据库、缓存、编辑器、AI 与 API 工具，附官网与 GitHub 来源。'

function categoryDescription(category: DownloadCategory): string {
  const names = category.softwares
    .slice(0, 6)
    .map((software) => software.name)
    .join('、')
  const more = category.softwares.length > 6 ? ' 等' : ''
  return `${category.name} 开发工具下载：${names}${more}，共 ${category.softwares.length} 个软件，提供国内镜像、官网与 GitHub 地址，适用于 Windows 开发环境。`
}

const pagePath = computed(() =>
  routeCategory.value ? categoryPagePath(routeCategory.value) : '/downloads',
)

const pageTitle = computed(() =>
  routeCategory.value
    ? `${routeCategory.value.name} 下载 · 镜像、官网与 GitHub 地址 · DevEnv`
    : '开发环境下载站 · Java、Python、Node.js 镜像加速 · DevEnv',
)

const pageDescription = computed(() =>
  routeCategory.value ? categoryDescription(routeCategory.value) : downloadDescription,
)

const seoJsonLd = computed(() => {
  const category = routeCategory.value
  if (!category) {
    return [
      collectionSchema({
        name: '开发环境下载站',
        description: downloadDescription,
        path: '/downloads',
        items: downloadCategories.map((item) => ({
          name: item.name,
          url: absoluteUrl(categoryPagePath(item)),
        })),
      }),
      breadcrumbSchema([{ name: '首页', path: '/' }, { name: '开发环境下载站' }]),
    ]
  }

  return [
    collectionSchema({
      name: `${category.name} 下载`,
      description: categoryDescription(category),
      path: pagePath.value,
      items: category.softwares.map((software) => ({
        name: software.name,
        url:
          software.official_site ||
          software.versions[0]?.url ||
          absoluteUrl(pagePath.value),
      })),
    }),
    breadcrumbSchema([
      { name: '首页', path: '/' },
      { name: '开发环境下载站', path: '/downloads' },
      { name: category.name },
    ]),
  ]
})

useSeo({
  title: pageTitle,
  description: pageDescription,
  path: pagePath,
  jsonLd: seoJsonLd,
})

function viewFromHash(hash: string): string {
  const id = hash.replace(/^#/, '')
  if (id === 'all') return 'all'
  if (id && downloadCategories.some((category) => categoryAnchor(category) === id)) return id
  return 'all'
}

// 旧的 /downloads#cat-xxx 深链仍然可用；分类独立页以路由为准。
function syncFromHash(): void {
  if (routeCategory.value) return
  view.value = viewFromHash(window.location.hash)
}

onMounted(() => {
  syncFromHash()
  window.addEventListener('hashchange', syncFromHash)
})

onBeforeUnmount(() => window.removeEventListener('hashchange', syncFromHash))

watch(
  () => route.meta.category,
  () => {
    if (routeCategory.value) {
      view.value = categoryAnchor(routeCategory.value)
      query.value = ''
      return
    }
    syncFromHash()
  },
)

// 分类视图切换后内容高度会变化；等 DOM 更新完再回到列表顶部，
// 避免锚点跳转后因旧滚动位置落到页面底部。
watch(view, async () => {
  await nextTick()
  contentRef.value?.scrollIntoView({ block: 'start' })
})

// 有搜索词时跨分类匹配软件名 / 描述 / 分类名后按分类聚合；
// 无搜索词时按当前视图（全部分类或单个分类）展示。
const categories = computed<DownloadCategory[]>(() => {
  const q = keyword.value
  // 分类独立页只在当前分类内搜索，避免页面内容与标题 / canonical 不一致。
  const pool = routeCategory.value ? [routeCategory.value] : downloadCategories

  if (q) {
    const matched: DownloadCategory[] = []
    for (const category of pool) {
      const softwares = category.softwares.filter(
        (software) =>
          software.name.toLowerCase().includes(q) ||
          (software.desc || '').toLowerCase().includes(q) ||
          category.name.toLowerCase().includes(q),
      )
      if (softwares.length) matched.push({ ...category, softwares })
    }
    return matched
  }

  if (view.value === 'all') return pool
  return pool.filter((category) => categoryAnchor(category) === view.value)
})

const totalMatched = computed(() =>
  categories.value.reduce((total, category) => total + category.softwares.length, 0),
)

// 导航选中态：category 为空表示「全部」项。
function isActive(category: DownloadCategory | null): boolean {
  return category ? view.value === categoryAnchor(category) : view.value === 'all'
}
</script>

<template>
  <div class="downloads-page">
    <section class="downloads-hero">
      <div class="downloads-container">
        <nav v-if="routeCategory" class="downloads-breadcrumb" aria-label="面包屑">
          <RouterLink to="/">首页</RouterLink>
          <span aria-hidden="true">/</span>
          <RouterLink to="/downloads">开发环境下载站</RouterLink>
          <span aria-hidden="true">/</span>
          <span class="current">{{ routeCategory.name }}</span>
        </nav>
        <p class="eyebrow"><span class="pulse"></span> Downloads</p>
        <h1>{{ routeCategory ? routeCategory.name + ' 下载' : downloadData.sitename }}</h1>
        <p class="lead">{{ routeCategory ? pageDescription : downloadData.description }}</p>

        <div class="downloads-stats">
          <span><strong>{{ downloadStats.categories }}</strong> 个分类</span>
          <span><strong>{{ downloadStats.softwares }}</strong> 个软件</span>
          <span><strong>{{ downloadStats.versions }}</strong> 个版本</span>
        </div>

        <div class="downloads-search">
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <circle cx="11" cy="11" r="7" />
            <path d="m20 20-3.6-3.6" />
          </svg>
          <input
            v-model="query"
            type="search"
            placeholder="搜索软件名称 / 描述 / 分类…"
            aria-label="搜索软件"
            autocomplete="off"
            spellcheck="false"
          />
          <button
            v-if="query"
            type="button"
            class="search-clear"
            aria-label="清空搜索"
            @click="query = ''"
          >
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path d="M6 6l12 12M18 6 6 18" />
            </svg>
          </button>
        </div>
      </div>
    </section>

    <nav class="downloads-nav" aria-label="软件分类导航">
      <div class="downloads-container downloads-nav-inner">
        <RouterLink :class="{ active: isActive(null) }" to="/downloads">全部</RouterLink>
        <RouterLink
          v-for="category in downloadCategories"
          :key="category.name"
          :class="{ active: isActive(category) }"
          :to="categoryPagePath(category)"
        >
          {{ category.name }}
        </RouterLink>
      </div>
    </nav>

    <main ref="contentRef" class="downloads-container downloads-content">
      <p v-if="keyword && categories.length" class="search-info">
        搜索「{{ query.trim() }}」：找到 {{ totalMatched }} 个软件，涉及
        {{ categories.length }} 个分类
      </p>
      <p v-else-if="keyword" class="search-info">
        搜索「{{ query.trim() }}」：暂无匹配结果，换个关键词试试。
      </p>
      <p v-else-if="!categories.length" class="search-info">暂无匹配结果，换个关键词试试。</p>

      <CategorySection
        v-for="category in categories"
        :id="categoryAnchor(category)"
        :key="category.name"
        :category="category"
      />

      <footer class="downloads-foot">
        <p>
          链接均为外部地址，站点不托管安装包。发现失效链接？
          <a :href="issuesUrl" target="_blank" rel="noopener noreferrer">提交反馈</a> ·
          <a :href="dataFormatUrl" target="_blank" rel="noopener noreferrer">数据格式说明</a> ·
          <a :href="dataEditUrl" target="_blank" rel="noopener noreferrer">直接编辑数据</a>
        </p>
        <p class="downloads-credit">
          ©
          <a href="https://fireneb.com" target="_blank" rel="noopener noreferrer">FireNeb</a>
          · 开发环境下载站
        </p>
      </footer>
    </main>
  </div>
</template>

<style scoped>
.downloads-page {
  min-height: 100%;
}

.downloads-container {
  width: 100%;
  max-width: 1280px;
  margin: 0 auto;
  padding: 0 24px;
}

/* ---- 顶部介绍区 ---- */
.downloads-hero {
  position: relative;
  overflow: hidden;
  padding: 54px 0 36px;
  border-bottom: 1px solid var(--border);
  background: var(--hero-bg);
}

.downloads-hero::after {
  content: '';
  position: absolute;
  top: -220px;
  right: -120px;
  width: 620px;
  height: 480px;
  border-radius: 50%;
  background: radial-gradient(circle, var(--hero-glow) 0%, transparent 65%);
  pointer-events: none;
}

.downloads-hero > .downloads-container {
  position: relative;
  z-index: 1;
}

.downloads-breadcrumb {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 7px;
  margin-bottom: 14px;
  color: var(--muted);
  font-size: 12px;
}

.downloads-breadcrumb a {
  color: var(--muted);
  text-decoration: none;
}

.downloads-breadcrumb a:hover {
  color: var(--brand);
}

.downloads-breadcrumb .current {
  color: var(--text-soft);
}

.eyebrow {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  margin: 0 0 14px;
  color: var(--brand);
  font-size: 12.5px;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.pulse {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: var(--brand);
  box-shadow: 0 0 0 4px var(--brand-soft);
}

.downloads-hero h1 {
  margin: 0 0 12px;
  color: var(--text);
  font-size: clamp(30px, 4.4vw, 44px);
  letter-spacing: -0.03em;
  line-height: 1.1;
}

.lead {
  max-width: 640px;
  margin: 0;
  color: var(--text-soft);
  font-size: 15px;
  line-height: 1.75;
}

.downloads-stats {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 22px;
  margin: 18px 0 22px;
  color: var(--muted);
  font-size: 12.5px;
}

.downloads-stats strong {
  color: var(--text);
  font-size: 16px;
  font-weight: 700;
}

/* ---- 搜索框 ---- */
.downloads-search {
  display: flex;
  align-items: center;
  gap: 9px;
  width: min(560px, 100%);
  height: 44px;
  padding: 0 10px 0 12px;
  border: 1px solid var(--border);
  border-radius: 11px;
  background: var(--surface);
  transition: border-color 0.15s ease, box-shadow 0.15s ease;
}

.downloads-search:focus-within {
  border-color: var(--brand);
  box-shadow: 0 0 0 3px var(--brand-soft);
}

.downloads-search > svg {
  width: 17px;
  height: 17px;
  flex-shrink: 0;
  fill: none;
  stroke: var(--muted);
  stroke-width: 1.8;
  stroke-linecap: round;
}

.downloads-search input {
  flex: 1;
  min-width: 0;
  border: 0;
  outline: none;
  background: transparent;
  color: var(--text);
  font: inherit;
  font-size: 14px;
}

.downloads-search input::-webkit-search-cancel-button {
  display: none;
}

.search-clear {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  padding: 0;
  border: 0;
  border-radius: 7px;
  background: var(--surface-2);
  color: var(--muted);
  cursor: pointer;
}

.search-clear:hover {
  color: var(--text);
}

.search-clear svg {
  width: 14px;
  height: 14px;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.8;
  stroke-linecap: round;
}

/* ---- 吸顶分类导航 ---- */
.downloads-nav {
  position: sticky;
  top: 64px;
  z-index: 30;
  border-bottom: 1px solid var(--border);
  background: color-mix(in srgb, var(--bg) 88%, transparent);
  backdrop-filter: saturate(1.4) blur(12px);
}

.downloads-nav-inner {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  padding-top: 10px;
  padding-bottom: 10px;
}

.downloads-nav a {
  padding: 4px 11px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--surface);
  color: var(--text-soft);
  font-size: 12.5px;
  text-decoration: none;
  transition: border-color 0.15s ease, color 0.15s ease, background 0.15s ease;
}

.downloads-nav a:hover {
  border-color: var(--brand);
  color: var(--brand);
}

.downloads-nav a.active {
  border-color: var(--brand);
  background: var(--brand);
  color: var(--on-brand);
  font-weight: 600;
}

/* ---- 列表 ---- */
.downloads-content {
  /* 与 .category 一致：滚动到列表顶部时避开站点头部与吸顶分类导航。 */
  scroll-margin-top: 126px;
  padding-top: 26px;
  padding-bottom: 72px;
}

.search-info {
  margin: 0 0 20px;
  color: var(--muted);
  font-size: 13px;
}

.downloads-foot {
  margin-top: 44px;
  padding-top: 20px;
  border-top: 1px solid var(--border);
  color: var(--muted);
  font-size: 12.5px;
  line-height: 1.7;
}

.downloads-foot p {
  margin: 0;
}

.downloads-credit {
  margin-top: 8px;
  font-size: 11.5px;
}

.downloads-foot a {
  color: var(--brand);
  text-decoration: none;
}

.downloads-foot a:hover {
  text-decoration: underline;
  text-underline-offset: 3px;
}

@media (max-width: 720px) {
  .downloads-container {
    padding: 0 16px;
  }

  .downloads-hero {
    padding: 40px 0 28px;
  }

  .downloads-nav {
    top: 64px;
  }

  .downloads-content {
    padding-top: 20px;
    padding-bottom: 56px;
  }
}
</style>
