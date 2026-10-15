import { useEffect, useState } from 'react'
import { api, fetchConfig, type AppConfig } from '@/lib/api'
import { mediaUrl } from '@/lib/media'
import { ContextMenu, type ContextMenuItem } from '@/components/ContextMenu'

type FolderItem = { Name?: string; FolderPath?: string; IconPath?: string }
type MenuState = { x: number; y: number; index: number } | null

export function FoldersPage() {
  const [items, setItems] = useState<FolderItem[]>([])
  const [config, setConfig] = useState<AppConfig | null>(null)
  const [menu, setMenu] = useState<MenuState>(null)

  async function load() {
    const { data } = await api.get<FolderItem[]>('/folders')
    setItems(Array.isArray(data) ? data : [])
  }

  useEffect(() => {
    void load()
    fetchConfig().then(setConfig).catch(() => undefined)
    const onChange = () => void load()
    window.addEventListener('devbox-shortcuts-changed', onChange)
    return () => window.removeEventListener('devbox-shortcuts-changed', onChange)
  }, [])

  async function open(index: number) {
    await api.post(`/folders/${index}/open`)
  }

  async function rename(index: number, current?: string) {
    const name = window.prompt('显示名称', current || '')
    if (name == null) return
    await api.put(`/folders/${index}`, { name })
    await load()
  }

  async function remove(index: number, name?: string) {
    if (!window.confirm(`移除「${name}」？`)) return
    await api.delete(`/folders/${index}`)
    await load()
  }

  function launch(index: number, clickCount: number) {
    const requireDouble = !!config?.DoubleClickToLaunch
    if (requireDouble && clickCount < 2) return
    if (!requireDouble && clickCount > 1) return
    void open(index)
  }

  function menuItems(index: number): ContextMenuItem[] {
    const item = items[index]
    return [
      { type: 'item', label: '打开', onClick: () => void open(index) },
      { type: 'item', label: '编辑别名与图标', onClick: () => void rename(index, item?.Name) },
      { type: 'separator' },
      { type: 'item', label: '移除', danger: true, onClick: () => void remove(index, item?.Name) },
    ]
  }

  return (
    <div
      className="h-full min-h-[400px] p-3"
      onContextMenu={(e) => {
        if ((e.target as HTMLElement).closest('[data-folder-card]'))
          return
        e.preventDefault()
        setMenu({ x: e.clientX, y: e.clientY, index: -1 })
      }}
    >
      {items.length === 0 ? (
        <div className="mx-auto mt-16 max-w-sm rounded-lg border border-gray-200 bg-white px-6 py-8 text-center shadow-sm">
          <div className="text-sm font-medium text-gray-800">暂无文件夹快捷方式</div>
          <div className="mt-1 text-xs text-gray-500">右键空白区域选择文件夹，或直接拖入文件夹</div>
        </div>
      ) : (
        <div className="flex flex-wrap gap-2 content-start">
          {items.map((item, index) => {
            const icon = mediaUrl(item.IconPath)
            return (
              <button
                key={`${item.FolderPath}-${index}`}
                type="button"
                data-folder-card
                title={item.FolderPath}
                className="flex h-14 w-[240px] items-center gap-2 rounded-md border border-gray-200 bg-white px-1.5 text-left shadow-sm hover:border-blue-300 hover:bg-blue-50/40 active:bg-blue-50"
                onClick={(e) => launch(index, e.detail)}
                onContextMenu={(e) => {
                  e.preventDefault()
                  e.stopPropagation()
                  setMenu({ x: e.clientX, y: e.clientY, index })
                }}
              >
                <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded bg-amber-50 text-amber-700">
                  {icon ? (
                    <img src={icon} alt="" className="h-6 w-6 object-contain" />
                  ) : (
                    <svg viewBox="0 0 24 24" className="h-5 w-5 fill-current" aria-hidden>
                      <path d="M10 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2h-8l-2-2z" />
                    </svg>
                  )}
                </span>
                <span className="min-w-0 flex-1 text-xs font-semibold leading-snug text-gray-800 line-clamp-2">
                  {item.Name || '未命名'}
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
                  label: '选择文件夹…',
                  onClick: async () => {
                    const { data } = await api.post<{ cancelled?: boolean; path?: string }>('/shell/pick-folder')
                    if (data.cancelled || !data.path) return
                    await api.post('/folders', { path: data.path })
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
