<script setup>
// 顶层布局：页头 + 分类导航 + 当前视图的分类区块 + 页脚
// 数据来自静态导入的 software.json（改 JSON 即改站点）
// 视图：导航按 URL hash 切换「全部」或单个分类；搜索时跨分类展示匹配结果
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import data from './data/software.json'
import AppHeader from './components/AppHeader.vue'
import AppFooter from './components/AppFooter.vue'
import CategorySection from './components/CategorySection.vue'

const query = ref('')

// 滚动容器（main）：整页不滚动，仅此区域滚动
const scrollEl = ref(null)

// 搜索词变化时，滚动区回到顶部，避免停留在旧结果的深处
watch(query, () => {
  scrollEl.value?.scrollTo({ top: 0 })
})

// 分类名 → 锚点 id（如 Node.js → cat-node-js），导航 hash 与区块 id 共用
// 优先取分类显式 id（software.json 中的 ASCII id，如 常用工具 → common-tools）：
// 中文放进 URL hash 在不同浏览器编码/解码不一致，会导致分类切换失效，显式 id 可彻底规避
// 无显式 id 时回退到名称 slug（保留中英文、折叠其余字符、去首尾 '-'）
function catId(name) {
  const cat = data.categories.find((c) => c.name === name)
  if (cat && cat.id) return 'cat-' + cat.id
  const slug = name
    .toLowerCase()
    .replace(/[^a-z0-9一-龥]+/g, '-')
    .replace(/^-+|-+$/g, '')
  return 'cat-' + slug
}

// 当前视图：'all' = 全部分类；'cat-xxx' = 单个分类，与 URL hash 同步（可刷新/分享/前进后退）
const view = ref('all')

function viewFromHash(hash) {
  const id = hash.replace(/^#/, '')
  if (id === 'all') return 'all'
  if (id && data.categories.some((c) => catId(c.name) === id)) return id
  return 'all'
}

function onHashChange() {
  view.value = viewFromHash(location.hash)
}

onMounted(() => {
  onHashChange()
  window.addEventListener('hashchange', onHashChange)
})
onBeforeUnmount(() => window.removeEventListener('hashchange', onHashChange))

const q = computed(() => query.value.trim().toLowerCase())

// 有搜索时跨分类匹配软件名/描述/分类名后按分类聚合；无搜索时按当前视图取分类
const categories = computed(() => {
  if (q.value) {
    return data.categories
      .map((cat) => {
        const matched = cat.softwares.filter((sw) => {
          return (
            sw.name.toLowerCase().includes(q.value) ||
            (sw.desc || '').toLowerCase().includes(q.value) ||
            cat.name.toLowerCase().includes(q.value)
          )
        })
        return matched.length ? { ...cat, softwares: matched } : null
      })
      .filter(Boolean)
  }
  if (view.value === 'all') return data.categories
  return data.categories.filter((c) => catId(c.name) === view.value)
})

const totalMatched = computed(() =>
  categories.value.reduce((n, c) => n + c.softwares.length, 0),
)

// 导航选中态：cat 为空表示「全部」项
function isActive(cat) {
  return cat ? view.value === catId(cat.name) : view.value === 'all'
}
</script>

<template>
  <div class="site">
    <AppHeader
      :site-name="data.sitename"
      :description="data.description"
      v-model:query="query"
    />

    <nav class="category-nav" aria-label="分类导航">
      <div class="container nav-inner">
        <a
          :class="{ active: isActive(null) }"
          href="#all"
        >全部</a>
        <a
          v-for="cat in data.categories"
          :key="cat.name"
          :class="{ active: isActive(cat) }"
          :href="'#' + catId(cat.name)"
        >{{ cat.name }}</a>
      </div>
    </nav>

    <main ref="scrollEl" class="container">
      <p v-if="q" class="search-info">
        搜索「{{ query.trim() }}」：找到 {{ totalMatched }} 个软件，涉及 {{ categories.length }} 个分类
      </p>
      <p v-else-if="!categories.length" class="search-info">暂无匹配结果，换个关键词试试。</p>

      <CategorySection
        v-for="cat in categories"
        :key="cat.name"
        :category="cat"
        :id="catId(cat.name)"
      />
    </main>

    <AppFooter :site-name="data.sitename" />
  </div>
</template>
