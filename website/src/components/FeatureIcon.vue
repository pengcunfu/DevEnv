<script setup lang="ts">
withDefaults(defineProps<{ name: string; size?: number }>(), {
  size: 24,
})
</script>

<template>
  <svg
    :width="size"
    :height="size"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="1.7"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
  >
    <g v-if="name === 'apps'">
      <rect x="4" y="4" width="6" height="6" rx="1.4" />
      <rect x="14" y="4" width="6" height="6" rx="1.4" />
      <rect x="4" y="14" width="6" height="6" rx="1.4" />
      <rect x="14" y="14" width="6" height="6" rx="1.4" />
    </g>
    <g v-else-if="name === 'lava'">
      <path d="M12 3.5c2.9 3.2 5 5.6 5 8.7a5 5 0 0 1-10 0c0-2.2 1.1-3.9 2.4-5.3" />
      <path d="M12 18.8a2.8 2.8 0 0 0 2.8-2.8c0-1.6-1.1-2.5-2.8-4.2-1.7 1.7-2.8 2.6-2.8 4.2a2.8 2.8 0 0 0 2.8 2.8z" />
    </g>
    <g v-else-if="name === 'translate'">
      <path d="M4 5h9" />
      <path d="M8.5 5v2c0 3-2 5.5-4.5 6.5" />
      <path d="M5 8.8c.9 2.1 2.7 3.6 5 4.4" />
      <path d="m13 19 3.5-9 3.5 9" />
      <path d="M14.3 15.8h4.4" />
    </g>
    <g v-else-if="name === 'network'">
      <path d="M3 12h4l2.5-6 4 12 2.5-6H21" />
    </g>
    <g v-else-if="name === 'process'">
      <rect x="7" y="7" width="10" height="10" rx="1.6" />
      <rect x="10" y="10" width="4" height="4" rx="0.8" />
      <path d="M9 3v4M15 3v4M9 17v4M15 17v4M3 9h4M3 15h4M17 9h4M17 15h4" />
    </g>
    <g v-else-if="name === 'service'">
      <rect x="4" y="4" width="16" height="6" rx="1.5" />
      <rect x="4" y="14" width="16" height="6" rx="1.5" />
      <path d="M7.5 7h.01M7.5 17h.01M12 7h4M12 17h4" />
    </g>
    <g v-else-if="name === 'clipboard'">
      <path d="M9 4.5h6v3H9z" />
      <path d="M8 6H6a2 2 0 0 0-2 2v11a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-2" />
      <path d="M9 12h6M9 16h4" />
    </g>
    <g v-else-if="name === 'tools'">
      <path d="M14.6 6.4a3.8 3.8 0 0 0-5.1 5L4 17l3 3 5.6-5.5a3.8 3.8 0 0 0 5-5.1l-2.2 2.2-2.5-2.5z" />
    </g>
    <g v-else-if="name === 'code'">
      <path d="M8.5 7.5 4 12l4.5 4.5" />
      <path d="M15.5 7.5 20 12l-4.5 4.5" />
      <path d="M13 5 11 19" />
    </g>
    <g v-else-if="name === 'download'">
      <path d="M12 4v11" />
      <path d="M7.5 10.5 12 15l4.5-4.5" />
      <path d="M5 20h14" />
    </g>
    <g v-else>
      <circle cx="12" cy="12" r="8" />
      <path d="M12 8v5l3 2" />
    </g>
  </svg>
</template>
