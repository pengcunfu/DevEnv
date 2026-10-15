<script setup>
// 软件卡片：名称/描述/来源徽章/官网链接/版本选择/下载（外链）/复制链接
import { ref, computed } from 'vue'
import SourceBadge from './SourceBadge.vue'
import SvgIcon from './SvgIcon.vue'

const props = defineProps({
  software: { type: Object, required: true },
})

const selected = ref(0)
const versions = computed(() => props.software.versions || [])
const current = computed(() => versions.value[selected.value] || {})

// source 优先取选中版本，缺省继承软件级 default_source
const source = computed(() => current.value.source || props.software.default_source || '')

const copied = ref(false)
async function copyLink() {
  const url = current.value.url
  try {
    await navigator.clipboard.writeText(url)
    copied.value = true
    setTimeout(() => (copied.value = false), 2000)
  } catch {
    // 非安全上下文等场景兜底：弹出输入框让用户手动复制
    window.prompt('请手动复制下载链接：', url)
  }
}
</script>

<template>
  <article class="card">
    <div class="card-head">
      <h3>{{ software.icon ? software.icon + ' ' : '' }}{{ software.name }}</h3>
      <SourceBadge :source="source" />
    </div>
    <p v-if="software.desc" class="desc">{{ software.desc }}</p>

    <select v-model.number="selected" class="version-select" :title="'选择 ' + software.name + ' 版本'">
      <option v-for="(v, i) in versions" :key="v.version" :value="i">
        v{{ v.version }}
      </option>
    </select>

    <p class="file-name" :title="current.file_name">{{ current.file_name || '—' }}</p>
    <p v-if="current.size" class="meta">大小：{{ current.size }}</p>
    <p v-if="current.os || current.arch" class="meta">
      平台：{{ current.os || '-' }}{{ current.arch ? ' / ' + current.arch : '' }}
    </p>
    <p v-if="current.remark" class="meta remark">※ {{ current.remark }}</p>

    <div class="actions">
      <a
        class="btn primary"
        :href="current.url"
        target="_blank"
        rel="noopener noreferrer"
      ><SvgIcon name="download" :size="14" />下载</a>
      <a
        v-if="software.official_site"
        class="btn"
        :href="software.official_site"
        target="_blank"
        rel="noopener noreferrer"
      >官网</a>
      <button class="btn" @click="copyLink">
        <SvgIcon v-if="copied" name="check" :size="14" />
        {{ copied ? '已复制' : '复制链接' }}
      </button>
    </div>
  </article>
</template>

<style scoped>
.card-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.card-head h3 {
  margin: 0;
  font-size: 17px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
