import { ref } from 'vue'

/** 全局浮层状态：移动端导航、文档侧栏抽屉、搜索面板。 */
export const mobileNavOpen = ref(false)
export const sidebarOpen = ref(false)
export const searchOpen = ref(false)

export function closeOverlays(): void {
  mobileNavOpen.value = false
  sidebarOpen.value = false
  searchOpen.value = false
}
