import { useCallback, useEffect, useState } from 'react'
import { api } from '@/lib/api'
import { mediaUrl } from '@/lib/media'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

type Entry = {
  id: number
  timestamp: string
  type: string
  text?: string
  imagePath?: string
  imageUrl?: string
  displayText: string
  isImage: boolean
}

const CLEAR_MODES = [
  { mode: 'all', label: '全部' },
  { mode: 'today', label: '今天' },
  { mode: 'week', label: '本周' },
  { mode: 'month', label: '本月' },
] as const

type Props = {
  /** 独立窗口模式：铺满窗口，无主壳入口说明 */
  standalone?: boolean
}

export function ClipboardPage({ standalone = false }: Props) {
  const [items, setItems] = useState<Entry[]>([])
  const [search, setSearch] = useState('')
  const [type, setType] = useState('')
  const [selected, setSelected] = useState<Entry | null>(null)
  const [message, setMessage] = useState('')

  const load = useCallback(async () => {
    const { data } = await api.get<Entry[]>('/clipboard', {
      params: { search: search || undefined, type: type || undefined },
    })
    setItems(data)
  }, [search, type])

  useEffect(() => {
    void load()
    const id = window.setInterval(() => void load(), 1500)
    return () => window.clearInterval(id)
  }, [load])

  useEffect(() => {
    const onRefresh = () => void load()
    window.addEventListener('devbox-clipboard-refresh', onRefresh)
    return () => window.removeEventListener('devbox-clipboard-refresh', onRefresh)
  }, [load])

  // 主壳内的入口页：打开独立窗口
  useEffect(() => {
    if (standalone) return
    void api.post('/shell/show-clipboard', { visible: true })
  }, [standalone])

  return (
    <div
      className={cn(
        'flex flex-col gap-3',
        standalone ? 'h-screen bg-[#f3f4f6] p-3' : 'h-full min-h-[480px]',
      )}
    >
      <div className="flex flex-wrap items-center gap-2">
        <div className="mr-auto">
          <h1 className="text-lg font-semibold">熔岩剪贴板</h1>
          <p className="text-sm text-gray-500">
            {standalone
              ? '自动记录文本 / 图片 · 双击还原 · Ctrl+Space 唤起'
              : '剪贴板在独立窗口中运行 · Ctrl+Space 唤起'}
          </p>
        </div>
        {!standalone && (
          <Button onClick={() => void api.post('/shell/show-clipboard', { visible: true })}>
            打开剪贴板窗口
          </Button>
        )}
        <input
          className="w-48 rounded-md border bg-white px-2 py-1.5 text-sm"
          placeholder="搜索…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <select className="rounded-md border bg-white px-2 py-1.5 text-sm" value={type} onChange={(e) => setType(e.target.value)}>
          <option value="">全部</option>
          <option value="text">文本</option>
          <option value="image">图片</option>
        </select>
        <select
          className="rounded-md border bg-white px-2 py-1.5 text-sm"
          defaultValue=""
          onChange={async (e) => {
            const mode = e.target.value
            e.target.value = ''
            if (!mode) return
            const label = CLEAR_MODES.find((m) => m.mode === mode)?.label ?? mode
            if (!window.confirm(`清空「${label}」历史？`)) return
            const { data } = await api.post<{ removed: number }>('/clipboard/clear', null, { params: { mode } })
            setMessage(`已删除 ${data.removed} 条`)
            setSelected(null)
            await load()
          }}
        >
          <option value="" disabled>清空…</option>
          {CLEAR_MODES.map((m) => (
            <option key={m.mode} value={m.mode}>{m.label}</option>
          ))}
        </select>
        <Button
          variant="outline"
          onClick={async () => {
            try {
              const { data } = await api.post<{ cancelled?: boolean; path?: string }>('/clipboard/export')
              setMessage(data.cancelled ? '已取消导出' : `已导出到 ${data.path}`)
            } catch (err) {
              const data = (err as { response?: { data?: { error?: string } } })?.response?.data
              setMessage(data?.error || (err instanceof Error ? err.message : '导出失败'))
            }
          }}
        >
          导出
        </Button>
      </div>
      {message && <div className="text-sm text-gray-500">{message}</div>}
      <div className="grid min-h-0 flex-1 grid-cols-[1fr_1.2fr] gap-3">
        <div className="overflow-auto rounded-lg border bg-white">
          {items.map((item) => (
            <button
              key={item.id}
              type="button"
              className="block w-full border-b px-3 py-2 text-left text-sm hover:bg-blue-50"
              onClick={() => setSelected(item)}
              onDoubleClick={async () => {
                await api.post(`/clipboard/${item.id}/paste`)
                setMessage('已写入系统剪贴板')
              }}
            >
              <div className="text-[11px] text-gray-400">{item.timestamp} · {item.type}</div>
              <div className="mt-0.5 line-clamp-2 whitespace-pre-wrap">{item.isImage ? '[图片]' : item.text}</div>
            </button>
          ))}
          {items.length === 0 && (
            <div className="px-3 py-8 text-center text-sm text-gray-500">暂无记录</div>
          )}
        </div>
        <div className="rounded-lg border bg-white p-3">
          {!selected ? (
            <p className="text-sm text-gray-500">选择一条记录查看详情（双击可还原到系统剪贴板）</p>
          ) : (
            <div className="space-y-3">
              <div className="text-xs text-gray-500">{selected.timestamp}</div>
              {selected.isImage ? (
                mediaUrl(selected.imageUrl || selected.imagePath) ? (
                  <img
                    src={mediaUrl(selected.imageUrl || selected.imagePath)}
                    alt=""
                    className="max-h-[360px] max-w-full rounded border object-contain"
                  />
                ) : (
                  <p className="text-sm">图片文件：{selected.imagePath}</p>
                )
              ) : (
                <pre className="max-h-[420px] overflow-auto whitespace-pre-wrap rounded bg-gray-50 p-3 text-sm">
                  {selected.text}
                </pre>
              )}
              <div className="flex gap-2">
                <Button
                  onClick={async () => {
                    await api.post(`/clipboard/${selected.id}/paste`)
                    setMessage('已写入系统剪贴板')
                  }}
                >
                  还原到剪贴板
                </Button>
                <Button
                  variant="outline"
                  onClick={async () => {
                    await api.delete(`/clipboard/${selected.id}`)
                    setSelected(null)
                    await load()
                  }}
                >
                  删除
                </Button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
