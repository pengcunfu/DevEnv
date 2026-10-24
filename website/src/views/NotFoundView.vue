<script setup lang="ts">
import { useHead } from '@unhead/vue'

useHead({
  title: '页面不存在 · DevEnv',
  meta: [{ name: 'robots', content: 'noindex' }],
})
</script>

<template>
  <section class="not-found">
    <p class="code">404</p>
    <h1>页面不存在</h1>
    <p>你访问的地址可能已经移动，或者链接拼写有误。</p>
    <div class="actions">
      <RouterLink class="btn btn-primary" to="/">回到首页</RouterLink>
      <RouterLink class="btn btn-secondary" to="/guide/getting-started">阅读文档</RouterLink>
    </div>
  </section>
</template>

<style scoped>
.not-found {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 64vh;
  padding: 60px 24px;
  text-align: center;
}

.code {
  margin: 0;
  background: linear-gradient(100deg, var(--brand), #38bdf8);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
  font-family: var(--font-mono);
  font-size: clamp(72px, 14vw, 130px);
  font-weight: 800;
  letter-spacing: -0.06em;
  line-height: 1;
}

h1 {
  margin: 10px 0 12px;
  color: var(--text);
  font-size: 24px;
}

.not-found > p {
  margin: 0 0 26px;
  color: var(--muted);
  font-size: 14px;
}

.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  justify-content: center;
}

.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  height: 42px;
  padding: 0 20px;
  border: 1px solid transparent;
  border-radius: 10px;
  font-size: 14px;
  font-weight: 650;
  text-decoration: none;
}

.btn-primary {
  background: var(--brand);
  color: var(--on-brand);
}

.btn-secondary {
  border-color: var(--border-strong);
  background: var(--surface);
  color: var(--text);
}
</style>
