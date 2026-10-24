<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import DocToc from '@/components/DocToc.vue'
import DocsSidebar from '@/components/DocsSidebar.vue'
import { sidebarOpen } from '@/composables/useUi'
import { renderDoc } from '@/docs/markdown'
import { editBase } from '@/docs/nav'
import { getDoc, getGroupEntryPath, getPrevNext } from '@/docs/registry'
import { articleSchema, breadcrumbSchema, faqSchema } from '@/seo/schema'
import { useSeo } from '@/seo/useSeo'

const route = useRoute()
const router = useRouter()
const articleRef = ref<HTMLElement>()
const activeHeading = ref('')
let observer: IntersectionObserver | undefined

const doc = computed(() => getDoc(route.path))
const rendered = computed(() => {
  const entry = doc.value
  return entry ? renderDoc(entry.raw, entry.dir) : { html: '', toc: [] }
})
const neighbors = computed(() => getPrevNext(route.path))
const editUrl = computed(() => (doc.value ? `${editBase}${doc.value.file}` : ''))
const groupPath = computed(() => (doc.value ? getGroupEntryPath(doc.value.group) : undefined))

const seoDescription = computed(() => doc.value?.description || '燧岚开发盒子（DevEnv）使用文档。')

const jsonLd = computed(() => {
  const entry = doc.value
  if (!entry) return undefined

  const breadcrumb: { name: string; path?: string }[] = [{ name: '首页', path: '/' }]
  if (groupPath.value && groupPath.value !== entry.path) {
    breadcrumb.push({ name: entry.group, path: groupPath.value })
  }
  breadcrumb.push({ name: entry.title })

  const nodes: Record<string, unknown>[] = [
    articleSchema({
      title: entry.title,
      description: entry.description,
      path: entry.path,
      group: entry.group,
    }),
    breadcrumbSchema(breadcrumb),
  ]

  if (entry.path === '/guide/lava/faq') {
    const faq = faqSchema(entry.raw, entry.path)
    if (faq) nodes.push(faq)
  }

  return nodes
})

useSeo({
  title: computed(() => (doc.value ? `${doc.value.title} · DevEnv 文档` : '文档 · DevEnv')),
  description: seoDescription,
  path: computed(() => route.path),
  type: 'article',
  section: computed(() => doc.value?.group),
  jsonLd,
})

function setupObserver(): void {
  observer?.disconnect()
  activeHeading.value = ''
  const article = articleRef.value
  if (!article || typeof IntersectionObserver === 'undefined') return

  const headings = Array.from(article.querySelectorAll<HTMLElement>('h2[id], h3[id]'))
  if (!headings.length) return

  observer = new IntersectionObserver(
    (entries) => {
      const visible = entries
        .filter((entry) => entry.isIntersecting)
        .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top)
      if (visible.length) activeHeading.value = visible[0].target.id
    },
    { rootMargin: '-88px 0px -70% 0px', threshold: 0 },
  )
  headings.forEach((heading) => observer?.observe(heading))
}

watch(
  () => route.path,
  async () => {
    sidebarOpen.value = false
    await nextTick()
    setupObserver()
  },
)

onMounted(async () => {
  await nextTick()
  setupObserver()
})

onBeforeUnmount(() => observer?.disconnect())

async function onArticleClick(event: MouseEvent): Promise<void> {
  const target = event.target as HTMLElement

  // 正文里的内部链接用路由跳转，避免整页刷新；保留新标签页 / 修饰键行为。
  const anchor = target.closest('a') as HTMLAnchorElement | null
  const href = anchor?.getAttribute('href')
  if (href && href.startsWith('/')) {
    if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey || event.button !== 0) {
      return
    }
    event.preventDefault()
    await router.push(href)
    return
  }

  const button = target.closest('.code-copy') as HTMLButtonElement | null
  if (!button) return

  const code = button.closest('.code-block')?.querySelector('code')
  if (!code) return

  try {
    await navigator.clipboard.writeText(code.textContent || '')
    button.textContent = '已复制'
  } catch {
    button.textContent = '复制失败'
  }
  window.setTimeout(() => {
    button.textContent = '复制'
  }, 1500)
}
</script>

<template>
  <div class="docs-shell">
    <div v-if="sidebarOpen" class="docs-overlay" @click="sidebarOpen = false"></div>

    <DocsSidebar :open="sidebarOpen" />

    <div class="docs-content">
      <button type="button" class="docs-dir-toggle" @click="sidebarOpen = true">
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M4 7h16M4 12h16M4 17h10" />
        </svg>
        文档目录
      </button>

      <nav class="breadcrumb" aria-label="面包屑">
        <RouterLink to="/">首页</RouterLink>
        <span aria-hidden="true">/</span>
        <RouterLink v-if="groupPath && groupPath !== doc?.path" :to="groupPath">
          {{ doc?.group }}
        </RouterLink>
        <span v-else>{{ doc?.group }}</span>
        <span aria-hidden="true">/</span>
        <span class="current">{{ doc?.title }}</span>
      </nav>

      <article
        ref="articleRef"
        class="markdown-body"
        v-html="rendered.html"
        @click="onArticleClick"
      ></article>

      <footer class="doc-footer">
        <a class="edit-link" :href="editUrl" target="_blank" rel="noopener noreferrer">
          在 GitHub 上编辑此页
        </a>

        <div class="doc-pager">
          <RouterLink
            v-if="neighbors.prev"
            class="pager-item prev"
            :to="neighbors.prev.path"
          >
            <span>← 上一篇</span>
            <strong>{{ neighbors.prev.title }}</strong>
          </RouterLink>
          <RouterLink
            v-if="neighbors.next"
            class="pager-item next"
            :to="neighbors.next.path"
          >
            <span>下一篇 →</span>
            <strong>{{ neighbors.next.title }}</strong>
          </RouterLink>
        </div>
      </footer>
    </div>

    <DocToc :items="rendered.toc" :active="activeHeading" />
  </div>
</template>

<style scoped>
.docs-shell {
  display: grid;
  grid-template-columns: 232px minmax(0, 1fr) 212px;
  gap: 40px;
  width: 100%;
  max-width: 1420px;
  margin: 0 auto;
  padding: 34px 24px 80px;
}

.docs-content {
  min-width: 0;
}

.docs-dir-toggle {
  display: none;
  align-items: center;
  gap: 7px;
  margin-bottom: 16px;
  padding: 7px 11px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--surface);
  color: var(--text-soft);
  font-size: 12.5px;
  cursor: pointer;
}

.docs-dir-toggle svg {
  width: 15px;
  height: 15px;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.8;
  stroke-linecap: round;
}

.breadcrumb {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 7px;
  margin-bottom: 18px;
  color: var(--muted);
  font-size: 12px;
}

.breadcrumb a {
  color: var(--muted);
  text-decoration: none;
}

.breadcrumb a:hover {
  color: var(--brand);
}

.breadcrumb .current {
  color: var(--text-soft);
}

.doc-footer {
  margin-top: 56px;
  padding-top: 22px;
  border-top: 1px solid var(--border);
}

.edit-link {
  color: var(--muted);
  font-size: 12.5px;
  text-decoration: none;
}

.edit-link:hover {
  color: var(--brand);
  text-decoration: underline;
  text-underline-offset: 3px;
}

.doc-pager {
  display: flex;
  gap: 14px;
  margin-top: 22px;
}

.pager-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
  flex: 1;
  min-width: 0;
  padding: 14px 16px;
  border: 1px solid var(--border);
  border-radius: 11px;
  background: var(--surface);
  text-decoration: none;
  transition: border-color 0.15s ease, transform 0.15s ease;
}

.pager-item:hover {
  border-color: var(--brand);
  transform: translateY(-1px);
}

.pager-item.next {
  align-items: flex-end;
  text-align: right;
}

.pager-item span {
  color: var(--muted);
  font-size: 11.5px;
}

.pager-item strong {
  overflow: hidden;
  color: var(--text);
  font-size: 13.5px;
  font-weight: 650;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 100%;
}

.docs-overlay {
  display: none;
}

@media (max-width: 1240px) {
  .docs-shell {
    grid-template-columns: 224px minmax(0, 1fr);
  }

  .docs-shell :deep(.docs-toc) {
    display: none;
  }
}

@media (max-width: 960px) {
  .docs-shell {
    grid-template-columns: minmax(0, 1fr);
    padding: 24px 20px 64px;
  }

  .docs-dir-toggle {
    display: inline-flex;
  }

  .docs-overlay {
    display: block;
    position: fixed;
    inset: 64px 0 0;
    z-index: 55;
    background: rgba(2, 6, 23, 0.4);
  }
}

@media (max-width: 560px) {
  .doc-pager {
    flex-direction: column;
  }
}
</style>
