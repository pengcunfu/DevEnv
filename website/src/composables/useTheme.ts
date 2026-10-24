export type Theme = 'light' | 'dark'

export function getTheme(): Theme {
  if (typeof document === 'undefined') return 'light'
  return document.documentElement.dataset.theme === 'dark' ? 'dark' : 'light'
}

export function toggleTheme(): Theme {
  const next: Theme = getTheme() === 'dark' ? 'light' : 'dark'
  document.documentElement.dataset.theme = next
  try {
    localStorage.setItem('devenv-theme', next)
  } catch {
    // 隐私模式下 localStorage 不可写，忽略即可。
  }
  return next
}
