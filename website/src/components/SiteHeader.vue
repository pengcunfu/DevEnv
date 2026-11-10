<script setup lang="ts">
import { useRoute } from 'vue-router'
import { closeOverlays, mobileNavOpen, searchOpen } from '@/composables/useUi'
import { toggleTheme } from '@/composables/useTheme'
import { primaryNav, repoUrl } from '@/docs/nav'

const route = useRoute()

function isActive(path: string): boolean {
  return route.path === path || (path !== '/' && route.path.startsWith(`${path}/`))
}

function openSearch(): void {
  searchOpen.value = true
  mobileNavOpen.value = false
}

function closeMobile(): void {
  closeOverlays()
}
</script>

<template>
  <header class="site-header">
    <div class="site-header-inner">
      <RouterLink to="/" class="brand" @click="closeMobile">
        <img src="/logo.svg" alt="" width="30" height="30" />
        <span class="brand-name">FireNeb DevEnv</span>
      </RouterLink>

      <nav class="site-nav" aria-label="主导航">
        <RouterLink
          v-for="item in primaryNav"
          :key="item.path"
          :to="item.path"
          class="site-nav-link"
          :class="{ active: isActive(item.path) }"
        >
          {{ item.text }}
        </RouterLink>
      </nav>

      <div class="header-actions">
        <button type="button" class="search-trigger" aria-label="搜索文档" @click="openSearch">
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <circle cx="11" cy="11" r="7" />
            <path d="m20 20-3.6-3.6" />
          </svg>
          <span class="search-trigger-text">搜索</span>
          <kbd>Ctrl K</kbd>
        </button>

        <button
          type="button"
          class="icon-btn theme-btn"
          aria-label="切换深浅色主题"
          title="切换深浅色主题"
          @click="toggleTheme"
        >
          <svg class="icon-moon" viewBox="0 0 24 24" aria-hidden="true">
            <path d="M20 14.5A8.5 8.5 0 0 1 9.5 4a8.5 8.5 0 1 0 10.5 10.5z" />
          </svg>
          <svg class="icon-sun" viewBox="0 0 24 24" aria-hidden="true">
            <circle cx="12" cy="12" r="4" />
            <path
              d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"
            />
          </svg>
        </button>

        <a
          class="icon-btn github-btn"
          :href="repoUrl"
          target="_blank"
          rel="noopener noreferrer"
          aria-label="GitHub 仓库"
          title="GitHub"
        >
          <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
            <path
              d="M12 .5C5.65.5.5 5.65.5 12c0 5.09 3.29 9.4 7.86 10.93.58.1.79-.25.79-.56v-2.16c-3.2.7-3.88-1.37-3.88-1.37-.52-1.33-1.28-1.69-1.28-1.69-1.05-.71.08-.7.08-.7 1.16.08 1.77 1.19 1.77 1.19 1.03 1.76 2.7 1.25 3.36.96.1-.75.4-1.25.73-1.54-2.55-.29-5.23-1.28-5.23-5.69 0-1.26.45-2.29 1.19-3.1-.12-.29-.52-1.46.11-3.05 0 0 .97-.31 3.17 1.18a11 11 0 0 1 5.78 0c2.2-1.49 3.17-1.18 3.17-1.18.63 1.59.23 2.76.11 3.05.74.81 1.19 1.84 1.19 3.1 0 4.42-2.69 5.39-5.25 5.68.41.35.78 1.05.78 2.12v3.14c0 .31.21.67.8.56A11.5 11.5 0 0 0 23.5 12C23.5 5.65 18.35.5 12 .5z"
            />
          </svg>
        </a>

        <button
          type="button"
          class="icon-btn menu-btn"
          aria-label="打开导航菜单"
          :aria-expanded="mobileNavOpen"
          @click="mobileNavOpen = !mobileNavOpen"
        >
          <svg v-if="!mobileNavOpen" viewBox="0 0 24 24" aria-hidden="true">
            <path d="M4 7h16M4 12h16M4 17h16" />
          </svg>
          <svg v-else viewBox="0 0 24 24" aria-hidden="true">
            <path d="M6 6l12 12M18 6 6 18" />
          </svg>
        </button>
      </div>
    </div>

    <Transition name="sheet">
      <nav v-if="mobileNavOpen" class="mobile-nav" aria-label="移动端导航">
        <RouterLink
          v-for="item in primaryNav"
          :key="item.path"
          :to="item.path"
          class="mobile-nav-link"
          :class="{ active: isActive(item.path) }"
          @click="closeMobile"
        >
          {{ item.text }}
        </RouterLink>
        <a :href="repoUrl" class="mobile-nav-link" target="_blank" rel="noopener noreferrer">
          GitHub
        </a>
      </nav>
    </Transition>
  </header>
</template>

<style scoped>
.site-header {
  position: sticky;
  top: 0;
  z-index: 50;
  border-bottom: 1px solid var(--border);
  background: var(--header-bg);
  backdrop-filter: saturate(1.4) blur(12px);
}

.site-header-inner {
  display: flex;
  align-items: center;
  gap: 28px;
  max-width: 1280px;
  height: 64px;
  margin: 0 auto;
  padding: 0 24px;
}

.brand {
  display: flex;
  align-items: center;
  gap: 9px;
  color: var(--text);
  text-decoration: none;
  flex-shrink: 0;
}

.brand img {
  border-radius: 8px;
}

.brand-name {
  font-size: 16px;
  font-weight: 700;
  letter-spacing: 0.01em;
}

.site-nav {
  display: flex;
  align-items: center;
  gap: 4px;
  margin-right: auto;
}

.site-nav-link {
  padding: 7px 11px;
  border-radius: 8px;
  color: var(--text-soft);
  font-size: 14px;
  text-decoration: none;
  transition: color 0.15s ease, background 0.15s ease;
}

.site-nav-link:hover {
  color: var(--text);
  background: var(--surface-2);
}

.site-nav-link.active {
  color: var(--brand);
  background: var(--brand-soft);
  font-weight: 600;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.search-trigger {
  display: flex;
  align-items: center;
  gap: 7px;
  height: 34px;
  padding: 0 9px 0 10px;
  border: 1px solid var(--border);
  border-radius: 9px;
  background: var(--surface);
  color: var(--muted);
  font-size: 13px;
  cursor: pointer;
  transition: border-color 0.15s ease, color 0.15s ease;
}

.search-trigger:hover {
  border-color: var(--border-strong);
  color: var(--text);
}

.search-trigger svg {
  width: 15px;
  height: 15px;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.8;
  stroke-linecap: round;
}

.search-trigger kbd {
  padding: 1px 5px;
  border: 1px solid var(--border);
  border-radius: 5px;
  background: var(--surface-2);
  font-family: var(--font-mono);
  font-size: 10px;
  line-height: 1.5;
}

.icon-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 34px;
  height: 34px;
  border: 1px solid transparent;
  border-radius: 9px;
  background: transparent;
  color: var(--text-soft);
  cursor: pointer;
  transition: background 0.15s ease, color 0.15s ease;
}

.icon-btn:hover {
  background: var(--surface-2);
  color: var(--text);
}

.icon-btn svg {
  width: 18px;
  height: 18px;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.8;
  stroke-linecap: round;
  stroke-linejoin: round;
}

.github-btn svg {
  fill: currentColor;
  stroke: none;
}

.menu-btn {
  display: none;
}

.mobile-nav {
  display: none;
}

@media (max-width: 1080px) {
  .site-nav {
    display: none;
  }

  .menu-btn {
    display: inline-flex;
  }

  .mobile-nav {
    display: flex;
    flex-direction: column;
    gap: 2px;
    padding: 10px 16px 16px;
    border-top: 1px solid var(--border);
    background: var(--surface);
  }

  .mobile-nav-link {
    padding: 11px 12px;
    border-radius: 9px;
    color: var(--text-soft);
    font-size: 15px;
    text-decoration: none;
  }

  .mobile-nav-link:hover,
  .mobile-nav-link.active {
    color: var(--brand);
    background: var(--brand-soft);
  }
}

@media (max-width: 640px) {
  .site-header-inner {
    gap: 12px;
    padding: 0 16px;
  }

  .search-trigger-text,
  .search-trigger kbd,
  .github-btn {
    display: none;
  }

  .search-trigger {
    width: 34px;
    padding: 0;
    justify-content: center;
  }
}
</style>
