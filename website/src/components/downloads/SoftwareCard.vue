<script setup lang="ts">
// 软件卡片：名称 / 来源徽章 / 描述 / 版本选择 / 下载（外链）/ 官网 / 复制链接。
import { computed, onBeforeUnmount, ref } from 'vue'
import type { DownloadSoftware, SoftwareVersion } from '@/downloads/software'
import SoftwareIcon from './SoftwareIcon.vue'
import SourceBadge from './SourceBadge.vue'

const props = defineProps<{ software: DownloadSoftware }>()

const selected = ref(0)
const copied = ref(false)
let copiedTimer: number | undefined

const versions = computed(() => props.software.versions || [])
const current = computed<SoftwareVersion | undefined>(() => versions.value[selected.value])

// source 优先取选中版本，缺省继承软件级 default_source。
const source = computed(() => current.value?.source || props.software.default_source || '')

async function copyLink(): Promise<void> {
  const url = current.value?.url
  if (!url) return

  try {
    if (!navigator.clipboard) throw new Error('clipboard unavailable')
    await navigator.clipboard.writeText(url)
    copied.value = true
    window.clearTimeout(copiedTimer)
    copiedTimer = window.setTimeout(() => {
      copied.value = false
    }, 2000)
  } catch {
    // 非安全上下文等场景兜底：弹出输入框让用户手动复制。
    window.prompt('请手动复制下载链接：', url)
  }
}

onBeforeUnmount(() => window.clearTimeout(copiedTimer))
</script>

<template>
  <article class="card">
    <div class="card-head">
      <h3>
        <span v-if="software.icon" aria-hidden="true">{{ software.icon }}</span>
        {{ software.name }}
      </h3>
      <SourceBadge :source="source" />
    </div>

    <p v-if="software.desc" class="desc">{{ software.desc }}</p>

    <select
      v-if="versions.length"
      v-model.number="selected"
      class="version-select"
      :aria-label="`选择 ${software.name} 版本`"
    >
      <option v-for="(item, index) in versions" :key="item.version" :value="index">
        v{{ item.version }}
      </option>
    </select>

    <p class="file-name" :title="current?.file_name">{{ current?.file_name || '—' }}</p>
    <p v-if="current?.size" class="meta">大小：{{ current.size }}</p>
    <p v-if="current?.os || current?.arch" class="meta">
      平台：{{ current.os || '-' }}{{ current.arch ? ' / ' + current.arch : '' }}
    </p>
    <p v-if="current?.remark" class="meta remark">※ {{ current.remark }}</p>

    <div class="actions">
      <a
        v-if="current?.url"
        class="btn primary"
        :href="current.url"
        target="_blank"
        rel="noopener noreferrer"
      >
        <SoftwareIcon name="download" :size="14" />
        下载
      </a>
      <a
        v-if="software.official_site"
        class="btn"
        :href="software.official_site"
        target="_blank"
        rel="noopener noreferrer"
      >
        官网
      </a>
      <button type="button" class="btn" @click="copyLink">
        <SoftwareIcon v-if="copied" name="check" :size="14" />
        {{ copied ? '已复制' : '复制链接' }}
      </button>
    </div>
  </article>
</template>

<style scoped>
.card {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 16px;
  border: 1px solid var(--border);
  border-radius: 13px;
  background: var(--surface);
  transition: border-color 0.18s ease, transform 0.18s ease, box-shadow 0.18s ease;
}

.card:hover {
  border-color: color-mix(in srgb, var(--brand) 45%, var(--border));
  transform: translateY(-2px);
  box-shadow: 0 12px 30px rgba(2, 6, 23, 0.07);
}

.card-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.card-head h3 {
  margin: 0;
  overflow: hidden;
  color: var(--text);
  font-size: 15px;
  font-weight: 650;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.desc {
  min-height: 2.6em;
  margin: 0;
  color: var(--muted);
  font-size: 12.5px;
  line-height: 1.6;
}

.version-select {
  width: 100%;
  height: 32px;
  padding: 0 8px;
  border: 1px solid var(--border);
  border-radius: 8px;
  outline: none;
  background: var(--surface-2);
  color: var(--text);
  font-size: 12.5px;
  cursor: pointer;
}

.version-select:focus-visible {
  border-color: var(--brand);
}

.file-name {
  margin: 0;
  overflow: hidden;
  color: var(--muted);
  font-family: var(--font-mono);
  font-size: 11px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.meta {
  margin: 0;
  color: var(--muted);
  font-size: 11.5px;
}

.remark {
  color: var(--warning);
}

.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: auto;
  padding-top: 4px;
}

.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  height: 31px;
  padding: 0 11px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--surface);
  color: var(--text-soft);
  font-family: inherit;
  font-size: 12px;
  text-decoration: none;
  cursor: pointer;
  transition: border-color 0.15s ease, color 0.15s ease, background 0.15s ease;
}

.btn:hover {
  border-color: var(--brand);
  color: var(--brand);
}

.btn.primary {
  border-color: var(--brand);
  background: var(--brand);
  color: var(--on-brand);
  font-weight: 600;
}

.btn.primary:hover {
  background: var(--brand-strong);
  border-color: var(--brand-strong);
  color: var(--on-brand);
}

@media (max-width: 560px) {
  .btn {
    flex: 1;
  }
}
</style>
