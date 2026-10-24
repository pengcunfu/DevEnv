<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import SearchDialog from '@/components/SearchDialog.vue'
import SiteFooter from '@/components/SiteFooter.vue'
import SiteHeader from '@/components/SiteHeader.vue'

const route = useRoute()
const isDocs = computed(() => route.path.startsWith('/guide/'))
</script>

<template>
  <div class="site-shell">
    <SiteHeader />
    <main class="site-main">
      <RouterView />
    </main>
    <SiteFooter v-if="!isDocs" />
    <SearchDialog />
  </div>
</template>
