<script setup>
// 粘性页头：站点名/副标题 + 搜索框 + FireNeb 官网链接
import { ref, watch } from 'vue'

defineProps({
  siteName: { type: String, default: '' },
  description: { type: String, default: '' },
  siteUrl: { type: String, default: 'https://fireneb.com' },
})

const query = defineModel('query', { type: String, default: '' })

// 移动端占位提示不参与搜索
const placeholder = '搜索软件名称 / 描述…'
</script>

<template>
  <header class="site-header">
    <div class="container header-inner">
      <div class="brand">
        <h1>{{ siteName }}</h1>
        <p v-if="description" class="subtitle">{{ description }}</p>
      </div>
      <div class="header-right">
        <input
          v-model="query"
          class="search-input"
          type="search"
          :placeholder="placeholder"
          aria-label="搜索软件"
        />
        <a
          class="site-link"
          :href="siteUrl"
          target="_blank"
          rel="noopener noreferrer"
        >FireNeb</a>
      </div>
    </div>
  </header>
</template>
