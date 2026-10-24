import { readFileSync } from 'node:fs'
import { fileURLToPath, URL } from 'node:url'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

// 版本号与桌面程序保持同一来源（仓库根 VERSION）。
const version = readFileSync(new URL('../VERSION', import.meta.url), 'utf8').trim()

export default defineConfig({
  base: '/',
  define: {
    __APP_VERSION__: JSON.stringify(version),
  },
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  plugins: [vue()],
  build: {
    target: 'es2020',
  },
  ssgOptions: {
    formatting: 'none',
    // 通配路由只在客户端兜底，不参与预渲染。
    includedRoutes: (paths: string[]) => paths.filter((path) => !path.includes(':')),
  },
})
