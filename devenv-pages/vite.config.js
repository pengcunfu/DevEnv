import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// base 使用 '/'：Cloudflare Pages 在根路径服务。
// 若将来部署到 GitHub Pages 项目子路径，改为 '/devenv-download/' 即可。
export default defineConfig({
  base: '/',
  plugins: [vue()],
  server: {
    port: 9997,
    strictPort: true,
  },
})
