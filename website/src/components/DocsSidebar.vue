<script setup lang="ts">
import { useRoute } from 'vue-router'
import { repoUrl, sidebarGroups } from '@/docs/nav'

defineProps<{ open: boolean }>()

const route = useRoute()

function isActive(path: string): boolean {
  return route.path === path
}
</script>

<template>
  <aside class="docs-sidebar" :class="{ open }">
    <nav aria-label="文档目录">
      <div v-for="group in sidebarGroups" :key="group.text" class="sidebar-group">
        <p class="sidebar-group-title">{{ group.text }}</p>
        <ul>
          <li v-for="item in group.items" :key="item.path">
            <RouterLink :to="item.path" :class="{ active: isActive(item.path) }">
              {{ item.text }}
            </RouterLink>
          </li>
        </ul>
      </div>
    </nav>

    <div class="sidebar-foot">
      <RouterLink to="/">← 回到官网首页</RouterLink>
      <a :href="repoUrl" target="_blank" rel="noopener noreferrer">GitHub 仓库</a>
    </div>
  </aside>
</template>

<style scoped>
.docs-sidebar {
  position: sticky;
  top: 88px;
  align-self: start;
  max-height: calc(100vh - 112px);
  overflow-y: auto;
  padding-right: 8px;
  scrollbar-width: thin;
}

.sidebar-group {
  margin-bottom: 22px;
}

.sidebar-group-title {
  margin: 0 0 7px;
  padding: 0 10px;
  color: var(--text);
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.02em;
}

.docs-sidebar ul {
  margin: 0;
  padding: 0;
  list-style: none;
}

.docs-sidebar li a {
  display: block;
  padding: 6px 10px;
  border-left: 2px solid transparent;
  border-radius: 0 7px 7px 0;
  color: var(--muted);
  font-size: 13px;
  text-decoration: none;
  transition: color 0.15s ease, background 0.15s ease, border-color 0.15s ease;
}

.docs-sidebar li a:hover {
  color: var(--text);
  background: var(--surface-2);
}

.docs-sidebar li a.active {
  border-left-color: var(--brand);
  background: var(--brand-soft);
  color: var(--brand);
  font-weight: 600;
}

.sidebar-foot {
  display: flex;
  flex-direction: column;
  gap: 7px;
  margin-top: 8px;
  padding: 14px 10px 4px;
  border-top: 1px solid var(--border);
}

.sidebar-foot a {
  color: var(--muted);
  font-size: 12px;
  text-decoration: none;
}

.sidebar-foot a:hover {
  color: var(--brand);
}

@media (max-width: 960px) {
  .docs-sidebar {
    position: fixed;
    top: 64px;
    bottom: 0;
    left: 0;
    z-index: 60;
    width: 280px;
    max-height: none;
    padding: 22px 16px 28px;
    border-right: 1px solid var(--border);
    background: var(--surface);
    transform: translateX(-102%);
    transition: transform 0.22s ease;
    overflow-y: auto;
  }

  .docs-sidebar.open {
    transform: translateX(0);
    box-shadow: 20px 0 60px rgba(2, 6, 23, 0.18);
  }
}
</style>
