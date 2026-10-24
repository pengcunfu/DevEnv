import { ViteSSG } from 'vite-ssg'
import App from './App.vue'
import { routes } from './router'
import './styles/main.css'

function decodeHash(hash: string): string {
  try {
    return decodeURIComponent(hash)
  } catch {
    return hash
  }
}

export const createApp = ViteSSG(
  App,
  {
    routes,
    scrollBehavior(to, _from, savedPosition) {
      if (savedPosition) return savedPosition
      if (to.hash) {
        return { el: decodeHash(to.hash), top: 88, behavior: 'smooth' }
      }
      return { top: 0 }
    },
  },
  ({ isClient }) => {
    if (!isClient) return
    // 主题由 index.html 的内联脚本在首屏前写入，路由切换无需额外处理。
  },
)
