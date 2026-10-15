import { useEffect, useState } from 'react'
import { api, fetchConfig, type AppConfig } from '@/lib/api'
import { mediaUrl } from '@/lib/media'
import { ContextMenu, type ContextMenuItem } from '@/components/ContextMenu'

type NativeApp = {
  Name?: string
  ExecPath?: string
  Description?: string
  IconPath?: string
}

type MenuState = { x: number; y: number; index: number } | null

export function AppsPage() {
  const [items, setItems] = useState<NativeApp[]>([])
  const [error, setError] = useState('')
  const [config, setConfig] = useState<AppConfig | null>(null)
  const [menu, setMenu] = useState<MenuState>(null)

  async function load() {
    try {
      const { data } = await api.get<NativeApp[]>('/native-apps')
      setItems(Array.isArray(data) ? data : [])
      setError('')
    } catch (err) {
      setError(err instanceof Error ? err.message : '加载失败')
    }
  }

  useEffect(() => {
    void load()
    fetchConfig().then(setConfig).catch(() => undefined)
    const onChange = () => void load()
    window.addEventListener('devbox-shortcuts-changed', onChange)
    return () => window.removeEventListener('devbox-shortcuts-changed', onChange)
  }, [])

  async function run(index: number) {
    await api.post(`/native-apps/${index}/run`)
  }

  async function rename(index: number, current?: string) {
    const name = window.prompt('显示名称', current || '')
    if (name == null) return
    await api.put(`/native-apps/${index}`, { name })
    await load()
  }

  async function remove(index: number, name?: string) {
    if (!window.confirm(`移除「${name}」？`)) return
    await api.delete(`/native-apps/${index}`)
    await load()
  }

  function launch(index: number, clickCount: number) {
    const requireDouble = !!config?.DoubleClickToLaunch
    if (requireDouble && clickCount < 2) return
    if (!requireDouble && clickCount > 1) return
    void run(index)
  }

  function menuItems(index: number): ContextMenuItem[] {
    const app = items[index]
    return [
      { type: 'item', label: '运行', onClick: () => void run(index) },
      { type: 'item', label: '编辑别名与图标', onClick: () => void rename(index, app?.Name) },
      { type: 'item', label: '在资源管理器中显示', onClick: () => void api.post(`/native-apps/${index}/open-folder`) },
      {
        type: 'item',
        label: '刷新图标',
        onClick: async () => {
          await api.put(`/native-apps/${index}`, { refreshIcon: true })
          await load()
        },
      },
      { type: 'separator' },
      { type: 'item', label: '移除', danger: true, onClick: () => void remove(index, app?.Name) },
    ]
  }

  return (
    <div
      className="h-full min-h-[400px] p-3"
      onContextMenu={(e) => {
        if ((e.target as HTMLElement).closest('[data-app-card]'))
          return
        e.preventDefault()
        setMenu({ x: e.clientX, y: e.clientY, index: -1 })
      }}
    >
      {error && (
        <div className="mb-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {error}
        </div>
      )}

      {items.length === 0 ? (
        <div className="mx-auto mt-16 max-w-sm rounded-lg border border-gray-200 bg-white px-6 py-8 text-center shadow-sm">
          <div className="text-sm font-medium text-gray-800">暂无本地应用程序</div>
          <div className="mt-1 text-xs text-gray-500">右键空白区域选择程序，或直接拖入 .exe</div>
        </div>
      ) : (
        <div className="flex flex-wrap gap-2 content-start">
          {items.map((app, index) => {
            const icon = mediaUrl(app.IconPath)
            const title = app.Name || '未命名'
            return (
              <button
                key={`${app.ExecPath}-${index}`}
                type="button"
                data-app-card
                title={app.ExecPath}
                className="flex h-14 w-[240px] items-center gap-2 rounded-md border border-gray-200 bg-white px-1.5 text-left shadow-sm hover:border-blue-300 hover:bg-blue-50/40 active:bg-blue-50"
                onClick={(e) => launch(index, e.detail)}
                onContextMenu={(e) => {
                  e.preventDefault()
                  e.stopPropagation()
                  setMenu({ x: e.clientX, y: e.clientY, index })
                }}
              >
                <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded bg-gray-100">
                  {icon ? (
                    <img src={icon} alt="" className="h-6 w-6 object-contain" />
                  ) : (
                    <span className="text-[10px] text-gray-400">EXE</span>
                  )}
                </span>
                <span className="min-w-0 flex-1 text-xs font-semibold leading-snug text-gray-800 line-clamp-2">
                  {title}
                </span>
              </button>
            )
          })}
        </div>
      )}

      {menu && (
        <ContextMenu
          x={menu.x}
          y={menu.y}
          onClose={() => setMenu(null)}
          items={
            menu.index < 0
              ? [{
                  type: 'item',
                  label: '选择程序…',
                  onClick: async () => {
                    const { data } = await api.post<{ cancelled?: boolean; path?: string }>('/shell/pick-exe')
                    if (data.cancelled || !data.path) return
                    await api.post('/native-apps', { path: data.path })
                    await load()
                  },
                }]
              : menuItems(menu.index)
          }
        />
      )}
    </div>
  )
}
