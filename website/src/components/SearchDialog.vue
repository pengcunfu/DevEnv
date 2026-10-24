<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { searchOpen } from '@/composables/useUi'
import { docs, searchSections, type SearchSection } from '@/docs/registry'

interface SearchResult {
  section: SearchSection
  score: number
  snippet: string
}

const router = useRouter()
const query = ref('')
const activeIndex = ref(0)
const inputRef = ref<HTMLInputElement>()

const escapeHtml = (text: string): string =>
  text.replace(/[&<>"']/g, (char) => {
    const map: Record<string, string> = {
      '&': '&amp;',
      '<': '&lt;',
      '>': '&gt;',
      '"': '&quot;',
      "'": '&#39;',
    }
    return map[char]
  })

function highlight(text: string, keyword: string): string {
  if (!keyword) return escapeHtml(text)
  const index = text.toLowerCase().indexOf(keyword.toLowerCase())
  if (index < 0) return escapeHtml(text)
  return (
    escapeHtml(text.slice(0, index)) +
    `<mark>${escapeHtml(text.slice(index, index + keyword.length))}</mark>` +
    escapeHtml(text.slice(index + keyword.length))
  )
}

function makeSnippet(text: string, keyword: string): string {
  const index = text.toLowerCase().indexOf(keyword.toLowerCase())
  const start = index < 0 ? 0 : Math.max(0, index - 60)
  const end = index < 0 ? 120 : Math.min(text.length, index + keyword.length + 90)
  return `${start > 0 ? '…' : ''}${highlight(text.slice(start, end), keyword)}${
    end < text.length ? '…' : ''
  }`
}

const results = computed<SearchResult[]>(() => {
  const keyword = query.value.trim().toLowerCase()
  if (!keyword) return []

  const matches: SearchResult[] = []
  for (const section of searchSections) {
    const headingMatch = section.heading.toLowerCase().includes(keyword)
    const bodyIndex = section.text.toLowerCase().indexOf(keyword)
    if (!headingMatch && bodyIndex < 0) continue

    matches.push({
      section,
      score: (headingMatch ? 100 : 0) + (bodyIndex >= 0 ? 10 : 0),
      snippet: makeSnippet(section.text, keyword),
    })
  }

  return matches.sort((a, b) => b.score - a.score).slice(0, 20)
})

function close(): void {
  searchOpen.value = false
}

function move(delta: number): void {
  if (!results.value.length) return
  const total = results.value.length
  activeIndex.value = (activeIndex.value + delta + total) % total
}

function go(section: SearchSection): void {
  const target = section.docPath + (section.id ? `#${section.id}` : '')
  close()
  router.push(target)
}

function selectActive(): void {
  const current = results.value[activeIndex.value]
  if (current) go(current.section)
}

function onGlobalKeydown(event: KeyboardEvent): void {
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
    event.preventDefault()
    searchOpen.value = !searchOpen.value
  }
}

watch(query, () => {
  activeIndex.value = 0
})

watch(searchOpen, async (open) => {
  if (typeof document === 'undefined') return
  document.body.style.overflow = open ? 'hidden' : ''
  if (!open) return
  query.value = ''
  activeIndex.value = 0
  await nextTick()
  inputRef.value?.focus()
})

onMounted(() => {
  window.addEventListener('keydown', onGlobalKeydown)
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', onGlobalKeydown)
  if (typeof document !== 'undefined') document.body.style.overflow = ''
})
</script>

<template>
  <Teleport v-if="searchOpen" to="body">
    <div class="search-overlay" @click.self="close">
      <div class="search-panel" role="dialog" aria-modal="true" aria-label="搜索文档">
        <div class="search-field">
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <circle cx="11" cy="11" r="7" />
            <path d="m20 20-3.6-3.6" />
          </svg>
          <input
            ref="inputRef"
            v-model="query"
            type="search"
            placeholder="搜索文档、功能或配置项…"
            autocomplete="off"
            spellcheck="false"
            @keydown.down.prevent="move(1)"
            @keydown.up.prevent="move(-1)"
            @keydown.enter.prevent="selectActive"
            @keydown.esc="close"
          />
          <button type="button" class="search-close" aria-label="关闭搜索" @click="close">
            Esc
          </button>
        </div>

        <div class="search-results">
          <template v-if="query && results.length">
            <button
              v-for="(result, index) in results"
              :key="result.section.docPath + result.section.id"
              type="button"
              class="search-result"
              :class="{ active: index === activeIndex }"
              @mousemove="activeIndex = index"
              @click="go(result.section)"
            >
              <span class="search-result-head">
                <span
                  class="search-result-title"
                  v-html="highlight(result.section.heading, query.trim())"
                ></span>
                <span class="search-result-crumb">
                  {{ result.section.group }} / {{ result.section.docTitle }}
                </span>
              </span>
              <span class="search-result-snippet" v-html="result.snippet"></span>
            </button>
          </template>
          <p v-else-if="query" class="search-empty">没有找到相关内容，换个关键词试试。</p>
          <p v-else class="search-empty">
            输入关键词搜索 {{ docs.length }} 篇文档，例如「环境」「热键」「镜像」。
          </p>
        </div>

        <div class="search-foot">
          <span><kbd>↑</kbd><kbd>↓</kbd> 选择</span>
          <span><kbd>Enter</kbd> 打开</span>
          <span><kbd>Esc</kbd> 关闭</span>
        </div>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.search-overlay {
  position: fixed;
  inset: 0;
  z-index: 100;
  display: flex;
  justify-content: center;
  align-items: flex-start;
  padding: 12vh 20px 20px;
  background: rgba(2, 6, 23, 0.55);
  backdrop-filter: blur(3px);
}

.search-panel {
  display: flex;
  flex-direction: column;
  width: min(680px, 100%);
  max-height: min(70vh, 640px);
  overflow: hidden;
  border: 1px solid var(--border);
  border-radius: 16px;
  background: var(--surface);
  box-shadow: 0 28px 80px rgba(2, 6, 23, 0.35);
}

.search-field {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 14px 16px;
  border-bottom: 1px solid var(--border);
}

.search-field svg {
  width: 18px;
  height: 18px;
  flex-shrink: 0;
  fill: none;
  stroke: var(--muted);
  stroke-width: 1.8;
  stroke-linecap: round;
}

.search-field input {
  flex: 1;
  min-width: 0;
  border: 0;
  outline: none;
  background: transparent;
  color: var(--text);
  font: inherit;
  font-size: 15px;
}

.search-field input::-webkit-search-cancel-button {
  display: none;
}

.search-close {
  padding: 3px 7px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--surface-2);
  color: var(--muted);
  font-family: var(--font-mono);
  font-size: 11px;
  cursor: pointer;
}

.search-results {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
}

.search-result {
  display: flex;
  flex-direction: column;
  gap: 5px;
  width: 100%;
  padding: 10px 12px;
  border: 0;
  border-radius: 10px;
  background: transparent;
  color: var(--text);
  text-align: left;
  cursor: pointer;
}

.search-result.active {
  background: var(--brand-soft);
}

.search-result-head {
  display: flex;
  align-items: baseline;
  gap: 10px;
}

.search-result-title {
  font-size: 14px;
  font-weight: 650;
}

.search-result-crumb {
  color: var(--muted);
  font-size: 11px;
}

.search-result-snippet {
  color: var(--muted);
  font-size: 12.5px;
  line-height: 1.6;
}

.search-result :deep(mark) {
  border-radius: 3px;
  background: color-mix(in srgb, var(--accent) 30%, transparent);
  color: inherit;
}

.search-empty {
  margin: 0;
  padding: 34px 16px;
  color: var(--muted);
  font-size: 13px;
  text-align: center;
}

.search-foot {
  display: flex;
  gap: 18px;
  padding: 9px 16px;
  border-top: 1px solid var(--border);
  background: var(--surface-2);
  color: var(--muted);
  font-size: 11px;
}

.search-foot kbd {
  margin-right: 3px;
  padding: 1px 5px;
  border: 1px solid var(--border);
  border-radius: 4px;
  background: var(--surface);
  font-family: var(--font-mono);
  font-size: 10px;
}
</style>
