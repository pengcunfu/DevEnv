<script setup>
// 通用线条图标组件：按 name 渲染对应 SVG 图标，颜色继承 currentColor
// 所有图标均为 24×24 viewBox 的描边风格，内容硬编码于此（无外部输入）
import { computed } from 'vue'

const props = defineProps({
  name: { type: String, default: '' },
  size: { type: [Number, String], default: 16 },
})

// 图标库：key 与 software.json 中 category.icon 的取值对应
const ICONS = {
  // 终端/代码（Go 等语言类）
  go: `<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M7 9l3 3-3 3"/><path d="M12 15h5"/>`,
  // 靶心（.NET 等框架类）
  dotnet: `<circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="5"/><circle cx="12" cy="12" r="1"/>`,
  // 编辑/笔（编辑器类）
  editors: `<path d="M12 20h9"/><path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4 12.5-12.5z"/>`,
  // 机器人（AI 工具类）
  ai: `<rect x="4" y="8" width="16" height="12" rx="2"/><path d="M12 4v4"/><circle cx="12" cy="4" r="2"/><path d="M9 14h.01"/><path d="M15 14h.01"/><path d="M9 17h6"/>`,
  // 烧瓶/实验（API 测试工具类）
  api: `<path d="M9 3h6"/><path d="M10 3v5.5L4.5 18a2 2 0 0 0 1.8 3h11.4a2 2 0 0 0 1.8-3L14 8.5V3"/><path d="M8 14h8"/>`,
  // 扳手（开发工具类）
  devtools: `<path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"/>`,
  // 盒子/打包（常用工具类）
  utils: `<path d="M21 8l-9-5-9 5v8l9 5 9-5V8z"/><path d="M3 8l9 5 9-5"/><path d="M12 13v8"/>`,
  // 齿轮/设置（打包构建类）
  pack: `<circle cx="12" cy="12" r="3"/><path d="M12 2.5V5M12 19v2.5M4.2 4.2L6 6M18 18l1.8 1.8M2.5 12H5M19 12h2.5M4.2 19.8L6 18M18 6l1.8-1.8"/>`,
  // 星标（页脚 Star 提示）
  star: `<polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/>`,
  // 下载箭头
  download: `<path d="M12 3v12"/><path d="M7 10l5 5 5-5"/><path d="M4 21h16"/>`,
  // 对勾（复制成功）
  check: `<path d="M4 12.5l5 5L20 6.5"/>`,
  // 未知 key 兜底：问号圆
  fallback: `<circle cx="12" cy="12" r="9"/><path d="M9.3 9a2.8 2.8 0 1 1 4.2 2.5c-.9.5-1.2 1-1.2 1.7"/><path d="M12 16.5h.01"/>`,
}

const current = computed(() => ICONS[props.name] || ICONS.fallback)

// 描边随尺寸等比缩放，小尺寸图标不至于糊成一团
const strokeWidth = computed(() => Math.max(1.2, props.size / 12))
</script>

<template>
  <svg
    class="svg-icon"
    xmlns="http://www.w3.org/2000/svg"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    :stroke-width="strokeWidth"
    stroke-linecap="round"
    stroke-linejoin="round"
    :width="size"
    :height="size"
    aria-hidden="true"
    v-html="current"
  />
</template>

<style scoped>
.svg-icon {
  display: inline-block;
  flex-shrink: 0;
  vertical-align: -0.15em;
}
</style>
