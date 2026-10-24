<script setup lang="ts">
import { issuesUrl, repoUrl, releasesUrl } from '@/docs/nav'

const version = __APP_VERSION__
</script>

<template>
  <footer class="site-footer">
    <div class="site-footer-inner">
      <div class="footer-brand">
        <div class="footer-logo">
          <img src="/logo.svg" alt="" width="34" height="34" />
          <div>
            <strong>程序盒子</strong>
            <span>DevEnv v{{ version }}</span>
          </div>
        </div>
        <p>
          Windows 桌面开发环境管理工具。把熔岩环境、网页翻译、网络与进程监控、剪贴板历史收进同一个盒子。
        </p>
        <p class="footer-license">Apache License 2.0 · 永久免费</p>
      </div>

      <div class="footer-col">
        <p class="footer-col-title">文档</p>
        <RouterLink to="/guide/getting-started">快速开始</RouterLink>
        <RouterLink to="/guide/features">功能概览</RouterLink>
        <RouterLink to="/guide/configuration">配置说明</RouterLink>
        <RouterLink to="/guide/lava/faq">熔岩常见问题</RouterLink>
      </div>

      <div class="footer-col">
        <p class="footer-col-title">项目</p>
        <a :href="repoUrl" target="_blank" rel="noopener noreferrer">GitHub 仓库</a>
        <a :href="releasesUrl" target="_blank" rel="noopener noreferrer">下载与发布</a>
        <RouterLink to="/downloads">软件下载站</RouterLink>
        <a :href="issuesUrl" target="_blank" rel="noopener noreferrer">问题反馈</a>
        <RouterLink to="/guide/build">从源码构建</RouterLink>
      </div>

      <div class="footer-col">
        <p class="footer-col-title">开发</p>
        <RouterLink to="/guide/releases">版本发布</RouterLink>
        <RouterLink to="/guide/license">开源协议</RouterLink>
        <a href="https://dotnet.microsoft.com/download" target="_blank" rel="noopener noreferrer">
          .NET 10
        </a>
        <a href="https://docs.devenv.cn/" target="_blank" rel="noopener noreferrer">在线文档</a>
      </div>
    </div>

    <div class="site-footer-bottom">
      <span>© 2025–2026 pengcunfu · 程序盒子 DevEnv</span>
      <span>基于 Vue 3 + Vite 构建 · 文档以 Markdown 编写</span>
    </div>
  </footer>
</template>

<style scoped>
.site-footer {
  border-top: 1px solid var(--border);
  background: var(--surface);
}

.site-footer-inner {
  display: grid;
  grid-template-columns: 1.8fr repeat(3, 1fr);
  gap: 48px;
  max-width: 1200px;
  margin: 0 auto;
  padding: 56px 24px 40px;
}

.footer-logo {
  display: flex;
  align-items: center;
  gap: 11px;
}

.footer-logo img {
  border-radius: 9px;
}

.footer-logo strong {
  display: block;
  font-size: 16px;
}

.footer-logo span {
  color: var(--muted);
  font-size: 12px;
}

.footer-brand p {
  max-width: 340px;
  margin: 16px 0 0;
  color: var(--muted);
  font-size: 13px;
  line-height: 1.75;
}

.footer-brand .footer-license {
  margin-top: 10px;
  color: var(--brand);
  font-size: 12px;
  font-weight: 600;
}

.footer-col {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.footer-col-title {
  margin: 0 0 3px;
  color: var(--text);
  font-size: 13px;
  font-weight: 700;
}

.footer-col a {
  color: var(--muted);
  font-size: 13px;
  text-decoration: none;
  transition: color 0.15s ease;
}

.footer-col a:hover {
  color: var(--brand);
}

.site-footer-bottom {
  display: flex;
  justify-content: space-between;
  gap: 16px;
  max-width: 1200px;
  margin: 0 auto;
  padding: 18px 24px 28px;
  border-top: 1px solid var(--border);
  color: var(--muted);
  font-size: 12px;
}

@media (max-width: 900px) {
  .site-footer-inner {
    grid-template-columns: 1fr 1fr;
    gap: 36px;
  }
}

@media (max-width: 560px) {
  .site-footer-inner {
    grid-template-columns: 1fr;
    padding-top: 40px;
  }

  .site-footer-bottom {
    flex-direction: column;
  }
}
</style>
