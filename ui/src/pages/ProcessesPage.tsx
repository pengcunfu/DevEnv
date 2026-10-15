import { useCallback, useEffect, useMemo, useState } from 'react'
import { api } from '@/lib/api'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

type ProcessRow = {
  name: string
  displayName: string
  processId: number
  cpuPercent: number
  cpuText: string
  workingSetBytes: number
  memoryText: string
  privateMemoryText: string
  virtualMemoryText: string
  threadCount: number
  handleCount: number
  path?: string
  pathText: string
  startTimeText: string
  respondingText: string
}

type Snapshot = {
  processCount: number
  totalMemoryText: string
  timestamp: string
  statusHint?: string
  processes: ProcessRow[]
}

type SortMode = 'cpu' | 'memory' | 'name'

export function ProcessesPage() {
  const [snapshot, setSnapshot] = useState<Snapshot | null>(null)
  const [query, setQuery] = useState('')
  const [sort, setSort] = useState<SortMode>('cpu')
  const [paused, setPaused] = useState(false)
  const [selectedPid, setSelectedPid] = useState<number | null>(null)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    try {
      const { data } = await api.get<Snapshot>('/processes', { params: { sort } })
      setSnapshot(data)
      setError('')
    } catch (err) {
      setError(err instanceof Error ? err.message : '加载失败')
    }
  }, [sort])

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    if (paused) return
    const id = window.setInterval(() => void load(), 1000)
    return () => window.clearInterval(id)
  }, [load, paused])

  const filtered = useMemo(() => {
    const list = snapshot?.processes ?? []
    if (!query.trim()) return list
    const q = query.trim().toLowerCase()
    return list.filter(
      (p) =>
        p.displayName.toLowerCase().includes(q)
        || String(p.processId).includes(q)
        || (p.pathText?.toLowerCase().includes(q) ?? false),
    )
  }, [snapshot, query])

  const selected = filtered.find((p) => p.processId === selectedPid) ?? null

  async function kill(tree: boolean) {
    if (!selected) return
    const label = tree ? '结束进程树' : '结束进程'
    if (!window.confirm(`确定要${label}「${selected.displayName}」(PID ${selected.processId}) 吗？`)) return
    try {
      await api.post(`/processes/${selected.processId}/kill`, null, { params: { tree } })
      setSelectedPid(null)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : '结束失败')
    }
  }

  async function openFolder() {
    if (!selected) return
    try {
      await api.post(`/processes/${selected.processId}/open-folder`)
    } catch (err) {
      setError(err instanceof Error ? err.message : '无法打开目录')
    }
  }

  return (
    <div className="flex h-full min-h-[480px] flex-col gap-3">
      <div>
        <h1 className="text-xl font-semibold text-gray-900">进程监控</h1>
        <p className="mt-1 text-sm text-gray-500">实时查看本机进程的 CPU、内存占用</p>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <input
          className="min-w-[220px] flex-1 rounded-md border border-gray-200 bg-white px-2 py-1.5 text-sm"
          placeholder="搜索进程名 / PID / 路径…"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
        <select
          className="rounded-md border border-gray-200 bg-white px-2 py-1.5 text-sm"
          value={sort}
          onChange={(e) => setSort(e.target.value as SortMode)}
        >
          <option value="cpu">按 CPU</option>
          <option value="memory">按内存</option>
          <option value="name">按名称</option>
        </select>
        <Button variant="outline" onClick={() => setPaused((v) => !v)}>
          {paused ? '继续' : '暂停'}
        </Button>
        <Button variant="outline" onClick={() => void load()}>
          立即刷新
        </Button>
        <Button variant="outline" disabled={!selected} onClick={() => void kill(false)}>
          结束进程
        </Button>
        <Button variant="outline" disabled={!selected} onClick={() => void kill(true)}>
          结束进程树
        </Button>
        <Button
          variant="outline"
          disabled={!selected?.path}
          onClick={() => void openFolder()}
        >
          打开目录
        </Button>
      </div>

      {error && <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>}

      <div className="grid min-h-0 flex-1 grid-cols-[1.6fr_1fr] gap-3">
        <div className="overflow-hidden rounded-lg border border-gray-200 bg-white">
          <div className="border-b border-gray-100 px-3 py-2 text-sm font-semibold">进程列表</div>
          <div className="max-h-[560px] overflow-auto">
            <table className="w-full text-left text-[12px]">
              <thead className="sticky top-0 bg-gray-50 text-gray-600">
                <tr>
                  <th className="px-2 py-1.5 font-medium">进程</th>
                  <th className="px-2 py-1.5 font-medium">PID</th>
                  <th className="px-2 py-1.5 font-medium">CPU</th>
                  <th className="px-2 py-1.5 font-medium">内存</th>
                  <th className="px-2 py-1.5 font-medium">线程</th>
                  <th className="px-2 py-1.5 font-medium">路径</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((p) => (
                  <tr
                    key={p.processId}
                    className={cn(
                      'cursor-pointer border-t border-gray-50 hover:bg-blue-50/60',
                      selectedPid === p.processId && 'bg-blue-50',
                    )}
                    onClick={() => setSelectedPid(p.processId)}
                  >
                    <td className="max-w-[140px] truncate px-2 py-1">{p.displayName}</td>
                    <td className="px-2 py-1 tabular-nums">{p.processId}</td>
                    <td className="px-2 py-1 tabular-nums">{p.cpuText}</td>
                    <td className="px-2 py-1 tabular-nums">{p.memoryText}</td>
                    <td className="px-2 py-1 tabular-nums">{p.threadCount}</td>
                    <td className="max-w-[220px] truncate px-2 py-1 text-gray-500">{p.pathText}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="rounded-lg border border-gray-200 bg-white p-3">
          <div className="mb-2 text-sm font-semibold">进程详情</div>
          {!selected ? (
            <p className="text-sm text-gray-500">未选择进程</p>
          ) : (
            <div className="space-y-1.5 text-sm text-gray-700">
              <div className="font-semibold">{selected.displayName}</div>
              <div className="text-gray-500">PID {selected.processId}</div>
              <div className="break-all text-gray-500">{selected.pathText || '路径不可用'}</div>
              <hr className="my-2 border-gray-100" />
              <div>CPU: {selected.cpuText}</div>
              <div>工作集: {selected.memoryText}</div>
              <div>专用内存: {selected.privateMemoryText}</div>
              <div>虚拟内存: {selected.virtualMemoryText}</div>
              <div>线程: {selected.threadCount}</div>
              <div>句柄: {selected.handleCount}</div>
              <div>启动时间: {selected.startTimeText}</div>
              <div>状态: {selected.respondingText}</div>
            </div>
          )}
        </div>
      </div>

      <div className="flex items-center justify-between rounded-lg border border-gray-200 bg-white px-3 py-2 text-xs text-gray-600">
        <span>
          进程数: {snapshot?.processCount ?? '—'}
          {snapshot?.statusHint ? `  |  ${snapshot.statusHint}` : ''}
        </span>
        <span className="flex gap-4">
          <span>
            工作集合计: <strong>{snapshot?.totalMemoryText ?? '—'}</strong>
          </span>
          <span>
            更新于{' '}
            {snapshot?.timestamp
              ? new Date(snapshot.timestamp).toLocaleTimeString()
              : '—'}
          </span>
        </span>
      </div>
    </div>
  )
}
