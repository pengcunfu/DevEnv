import { useEffect, useMemo, useState } from 'react'
import { api } from '@/lib/api'
import { cn } from '@/lib/utils'

const CATEGORY_ORDER = ['系统管理', '硬件与设备', '网络与安全', '性能与监控']

type Tool = {
  key: string
  name: string
  category: string
  description: string
  icon?: string
}

export function ToolsPage() {
  const [tools, setTools] = useState<Tool[]>([])
  const [query, setQuery] = useState('')
  const [message, setMessage] = useState('')

  useEffect(() => {
    api.get<Tool[]>('/system-tools').then((res) => setTools(res.data)).catch((err: unknown) => {
      setMessage(err instanceof Error ? err.message : '加载失败')
    })
  }, [])

  const grouped = useMemo(() => {
    const q = query.trim().toLowerCase()
    const filtered = !q
      ? tools
      : tools.filter(
          (t) =>
            t.name.toLowerCase().includes(q)
            || t.key.toLowerCase().includes(q)
            || t.description.toLowerCase().includes(q),
        )
    const map = new Map<string, Tool[]>()
    for (const tool of filtered) {
      const list = map.get(tool.category) ?? []
      list.push(tool)
      map.set(tool.category, list)
    }
    const ordered = CATEGORY_ORDER
      .filter((c) => map.has(c))
      .map((c) => [c, map.get(c)!] as const)
    for (const [c, list] of map) {
      if (!CATEGORY_ORDER.includes(c))
        ordered.push([c, list])
    }
    return ordered
  }, [tools, query])

  return (
    <div className="space-y-4 p-3">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <h1 className="text-lg font-semibold leading-tight">系统工具</h1>
          <p className="mt-0.5 text-sm text-gray-500">快速访问 Windows 系统管理工具</p>
        </div>
        <input
          className="mt-0.5 h-8 w-52 shrink-0 rounded-md border border-gray-200 bg-white px-2 text-sm"
          placeholder="搜索工具…"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
      </div>
      {message && <div className="text-sm text-red-600">{message}</div>}
      {grouped.map(([category, list]) => (
        <section key={category}>
          <h2 className="mb-2 text-sm font-semibold text-gray-800">{category}</h2>
          <div className="flex flex-wrap gap-2">
            {list.map((tool) => (
              <button
                key={tool.key}
                type="button"
                title={tool.description}
                className={cn(
                  'flex h-[88px] w-[100px] flex-col items-center justify-center gap-1 rounded-md border border-gray-200 bg-white px-2 py-2',
                  'hover:bg-gray-50 active:bg-blue-50',
                )}
                onClick={async () => {
                  try {
                    await api.post(`/system-tools/${tool.key}/run`)
                    setMessage(`已启动 ${tool.name}`)
                  } catch (err) {
                    const data = (err as { response?: { data?: { error?: string } } })?.response?.data
                    setMessage(data?.error || (err instanceof Error ? err.message : '启动失败'))
                  }
                }}
              >
                <span className="flex h-9 w-9 items-center justify-center rounded-md bg-gray-100">
                  <img
                    src={tool.icon || `/icons/win/${tool.key}.png`}
                    alt=""
                    className="h-7 w-7 object-contain"
                    onError={(e) => {
                      const img = e.currentTarget
                      if (!img.src.endsWith('/icons/win/default.png'))
                        img.src = '/icons/win/default.png'
                    }}
                  />
                </span>
                <span className="line-clamp-2 text-center text-[11px] leading-tight text-gray-800">
                  {tool.name}
                </span>
              </button>
            ))}
          </div>
        </section>
      ))}
    </div>
  )
}
