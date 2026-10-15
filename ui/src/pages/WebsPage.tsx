import { useEffect, useState } from 'react'
import { api, fetchConfig, type AppConfig } from '@/lib/api'
import { mediaUrl } from '@/lib/media'
import { ContextMenu, type ContextMenuItem } from '@/components/ContextMenu'

type WebItem = { Name?: string; Url?: string; IconPath?: string }
type MenuState = { x: number; y: number; index: number } | null

export function WebsPage() {
  const [items, setItems] = useState<WebItem[]>([])
  const [config, setConfig] = useState<AppConfig | null>(null)
  const [menu, setMenu] = useState<MenuState>(null)
  const [adding, setAdding] = useState(false)

  async function load() {
    const { data } = await api.get<WebItem[]>('/webs')
    setItems(Array.isArray(data) ? data : [])
  }

  useEffect(() => {
    void load()
    fetchConfig().then(setConfig).catch(() => undefined)
  }, [])

  async function addWeb() {
    const url = window.prompt('输入网址')
    if (!url) return
    const title = window.prompt('显示名称（可空，留空则自动抓取）') || undefined
    setAdding(true)
    try {
      await api.post('/webs', { url, title })
      await load()
    } finally {
      setAdding(false)
    }
  }

  async function open(index: number) {
    await api.post(`/webs/${index}/open`)
  }

  async function rename(index: number, current?: string) {
    const name = window.prompt('显示名称', current || '')
    if (name == null) return
    await api.put(`/webs/${index}`, { name })
    await load()
  }

  async function remove(index: number, name?: string) {
    if (!window.confirm(`移除「${name}」？`)) return
    await api.delete(`/webs/${index}`)
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
      { type: 'item', label: '在浏览器中打开', onClick: () => void open(index) },
      { type: 'item', label: '编辑标题与图标', onClick: () => void rename(index, item?.Name) },
      {
        type: 'item',
        label: '刷新图标',
        onClick: async () => {
          await api.put(`/webs/${index}`, { refreshIcon: true })
          await load()
        },
      },
      { type: 'separator' },
      { type: 'item', label: '移除', danger: true, onClick: () => void remove(index, item?.Name) },
    ]
  }

  return (
    <div
      className="h-full min-h-[400px] p-3"
      onContextMenu={(e) => {
        if ((e.target as HTMLElement).closest('[data-web-card]'))
          return
        e.preventDefault()
        setMenu({ x: e.clientX, y: e.clientY, index: -1 })
      }}
    >
      {items.length === 0 ? (
        <div className="mx-auto mt-16 max-w-sm rounded-lg border border-gray-200 bg-white px-6 py-8 text-center shadow-sm">
          <div className="text-sm font-medium text-gray-800">暂无快捷网站</div>
          <div className="mt-1 text-xs text-gray-500">
            {adding ? '正在抓取标题与图标…' : '右键空白区域添加网站'}
          </div>
        </div>
      ) : (
        <div className="flex flex-wrap gap-2 content-start">
          {items.map((item, index) => {
            const icon = mediaUrl(item.IconPath)
            return (
              <button
                key={`${item.Url}-${index}`}
                type="button"
                data-web-card
                title={item.Url}
                className="flex h-14 w-[240px] items-center gap-2 rounded-md border border-gray-200 bg-white px-1.5 text-left shadow-sm hover:border-blue-300 hover:bg-blue-50/40 active:bg-blue-50"
                onClick={(e) => launch(index, e.detail)}
                onContextMenu={(e) => {
                  e.preventDefault()
                  e.stopPropagation()
                  setMenu({ x: e.clientX, y: e.clientY, index })
                }}
              >
                <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded bg-sky-50">
                  {icon ? (
                    <img src={icon} alt="" className="h-6 w-6 object-contain" />
                  ) : (
                    <span className="text-[10px] text-sky-600">WEB</span>
                  )}
                </span>
                <span className="min-w-0 flex-1 text-xs font-semibold leading-snug text-gray-800 line-clamp-2">
                  {item.Name || item.Url || '未命名'}
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
              ? [{ type: 'item', label: '添加网站…', disabled: adding, onClick: () => void addWeb() }]
              : menuItems(menu.index)
          }
        />
      )}
    </div>
  )
}
