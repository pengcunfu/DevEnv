<script setup lang="ts">
import type { TocItem } from '@/docs/markdown'

defineProps<{ items: TocItem[]; active: string }>()

function scrollToHeading(id: string): void {
  const heading = document.getElementById(id)
  if (!heading) return
  const top = heading.getBoundingClientRect().top + window.scrollY - 84
  window.scrollTo({ top, behavior: 'smooth' })
  history.replaceState(null, '', `#${id}`)
}
</script>

<template>
  <aside v-if="items.length" class="docs-toc">
    <p class="toc-title">本页目录</p>
    <ul>
      <li
        v-for="item in items"
        :key="item.id"
        :class="[`level-${item.level}`, { active: active === item.id }]"
      >
        <a :href="`#${item.id}`" @click.prevent="scrollToHeading(item.id)">{{ item.text }}</a>
      </li>
    </ul>
  </aside>
</template>

<style scoped>
.docs-toc {
  position: sticky;
  top: 88px;
  align-self: start;
  max-height: calc(100vh - 112px);
  overflow-y: auto;
  padding-left: 6px;
  scrollbar-width: thin;
}

.toc-title {
  margin: 0 0 9px;
  color: var(--text);
  font-size: 12px;
  font-weight: 700;
}

.docs-toc ul {
  margin: 0;
  padding: 0;
  list-style: none;
  border-left: 1px solid var(--border);
}

.docs-toc li a {
  display: block;
  padding: 5px 0 5px 12px;
  margin-left: -1px;
  border-left: 1px solid transparent;
  color: var(--muted);
  font-size: 12.5px;
  line-height: 1.5;
  text-decoration: none;
  transition: color 0.15s ease, border-color 0.15s ease;
}

.docs-toc li.level-3 a {
  padding-left: 24px;
  font-size: 12px;
}

.docs-toc li a:hover {
  color: var(--text);
}

.docs-toc li.active a {
  border-left-color: var(--brand);
  color: var(--brand);
  font-weight: 600;
}
</style>
