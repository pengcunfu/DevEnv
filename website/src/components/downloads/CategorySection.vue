<script setup lang="ts">
// 分类区块：分类标题（图标 + 名称 + 数量）+ 软件卡片网格。
// id 由父组件通过 attribute fallthrough 传入，供分类导航锚点跳转。
import type { DownloadCategory } from '@/downloads/software'
import SoftwareCard from './SoftwareCard.vue'
import SoftwareIcon from './SoftwareIcon.vue'

defineProps<{ category: DownloadCategory }>()
</script>

<template>
  <section class="category">
    <h2 class="category-title">
      <SoftwareIcon v-if="category.icon" :name="category.icon" :size="20" class="category-icon" />
      <span>{{ category.name }}</span>
      <span class="count">{{ category.softwares.length }}</span>
    </h2>
    <div class="card-grid">
      <SoftwareCard v-for="software in category.softwares" :key="software.name" :software="software" />
    </div>
  </section>
</template>

<style scoped>
.category {
  /* 站点头部 64px + 吸顶分类导航高度，锚点跳转时避免被遮住。 */
  scroll-margin-top: 126px;
  margin-bottom: 32px;
}

.category-title {
  display: flex;
  align-items: center;
  gap: 7px;
  margin: 0 0 12px;
  color: var(--text);
  font-size: 16px;
  font-weight: 700;
}

.category-icon {
  color: var(--brand);
}

.count {
  padding: 0 7px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--surface);
  color: var(--muted);
  font-size: 11px;
  font-weight: 500;
}

.card-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 12px;
}

@media (max-width: 560px) {
  .card-grid {
    grid-template-columns: 1fr;
  }
}
</style>
