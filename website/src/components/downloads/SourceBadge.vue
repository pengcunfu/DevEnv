<script setup lang="ts">
// 来源徽章：按 source 文本映射颜色（含「镜像」→绿=国内高速，「官网」→蓝，「GitHub」→紫，其他→灰）。
import { computed } from 'vue'

const props = withDefaults(defineProps<{ source?: string }>(), {
  source: '',
})

const variant = computed(() => {
  const source = props.source
  if (
    source.includes('镜像') ||
    source.includes('阿里云') ||
    source.includes('华为云') ||
    source.includes('中科大')
  ) {
    return 'mirror'
  }
  if (source.includes('官网')) return 'official'
  if (source.includes('GitHub')) return 'github'
  return 'other'
})
</script>

<template>
  <span class="badge" :class="variant" :title="source || '其他'">{{ source || '其他' }}</span>
</template>

<style scoped>
.badge {
  align-self: flex-start;
  flex-shrink: 0;
  padding: 1px 7px;
  border-radius: 6px;
  font-size: 11px;
  line-height: 1.6;
  white-space: nowrap;
}

.badge.mirror {
  background: var(--badge-mirror-bg);
  color: var(--badge-mirror-fg);
}

.badge.official {
  background: var(--badge-official-bg);
  color: var(--badge-official-fg);
}

.badge.github {
  background: var(--badge-github-bg);
  color: var(--badge-github-fg);
}

.badge.other {
  background: var(--badge-other-bg);
  color: var(--badge-other-fg);
}
</style>
