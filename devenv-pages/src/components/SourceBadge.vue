<script setup>
// 来源徽章：按 source 文本映射颜色（含"镜像"→绿=国内高速，"官网"→蓝，"GitHub"→紫，其他→灰）
import { computed } from 'vue'

const props = defineProps({
  source: { type: String, default: '' },
})

const cls = computed(() => {
  const s = props.source
  if (s.includes('镜像') || s.includes('阿里云') || s.includes('华为云') || s.includes('中科大')) return 'mirror'
  if (s.includes('官网')) return 'official'
  if (s.includes('GitHub')) return 'github'
  return 'other'
})
</script>

<template>
  <span class="badge" :class="cls">{{ source || '其他' }}</span>
</template>
