<script setup>
// 页脚：单行版权信息，FireNeb 为可点击链接（跳转官网）
defineProps({
  siteName: { type: String, default: '' },
  siteUrl: { type: String, default: 'https://fireneb.com' },
})

const year = new Date().getFullYear()
</script>

<template>
  <footer class="site-footer">
    <div class="container">
      <p>© {{ year }} <a :href="siteUrl" target="_blank" rel="noopener noreferrer">FireNeb</a> · {{ siteName }}</p>
    </div>
  </footer>
</template>
