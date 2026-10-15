import { useCallback, useEffect, useMemo, useState } from 'react'
import { api } from '@/lib/api'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

type Conn = {
  protocol: string
  localEndpoint: string
  remoteEndpoint: string
  state: string
  downloadRateText: string
  uploadRateText: string
}

type Proc = {
  processId: number
  displayName: string
  executablePath?: string
  connectionCount: number
  downloadRateText: string
  uploadRateText: string
  connections: Conn[]
}

type Snapshot = {
  timestamp: string
  totalConnections: number
  totalDownloadText: string
  totalUploadText: string
  isElevated: boolean
  statusHint?: string
  processes: Proc[]
}

type PortRow = {
  protocol: string
  localAddress: string
  localPort: number
  remoteAddress: string
  remotePort: number
  state: string
  processId: number
  processName: string
}

type SpeedStatus = {
  running: boolean
  progress?: {
    phase: string
    percent: number
    overallPercent: number
    currentSpeedText: string
    message?: string
  }
  result?: {
    downloadText: string
    uploadText: string
    pingText: string
    jitterText: string
    serverName: string
    durationText: string
    uploadMessage?: string
  }
  error?: string
}

type Tab = 'monitor' | 'ports' | 'speed'

export function NetworkPage() {
  const [tab, setTab] = useState<Tab>('monitor')
  const [snapshot, setSnapshot] = useState<Snapshot | null>(null)
  const [query, setQuery] = useState('')
  const [paused, setPaused] = useState(false)
  const [selectedPid, setSelectedPid] = useState<number | null>(null)
  const [error, setError] = useState('')
  const [ports, setPorts] = useState<PortRow[]>([])
  const [portQuery, setPortQuery] = useState('')
  const [speed, setSpeed] = useState<SpeedStatus | null>(null)

  const load = useCallback(async () => {
    try {
      const { data } = await api.get<Snapshot>('/network/snapshot', { params: { sortByTraffic: true } })
      setSnapshot(data)
      setError('')
    } catch (err) {
      setError(err instanceof Error ? err.message : '加载失败')
    }
  }, [])

  const loadPorts = useCallback(async () => {
    const { data } = await api.get<PortRow[]>('/network/ports')
    setPorts(data)
  }, [])

  const loadSpeed = useCallback(async () => {
    const { data } = await api.get<SpeedStatus>('/network/speedtest/status')
    setSpeed(data)
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    if (paused || tab !== 'monitor') return
    const id = window.setInterval(() => void load(), 1000)
    return () => window.clearInterval(id)
  }, [load, paused, tab])

  useEffect(() => {
    if (tab !== 'ports') return
    void loadPorts()
    const id = window.setInterval(() => void loadPorts(), 2000)
    return () => window.clearInterval(id)
  }, [tab, loadPorts])

  useEffect(() => {
    if (tab !== 'speed') return
    void loadSpeed()
    const id = window.setInterval(() => void loadSpeed(), 500)
    return () => window.clearInterval(id)
  }, [tab, loadSpeed])

  const filtered = useMemo(() => {
    const list = snapshot?.processes ?? []
    if (!query.trim()) return list
    const q = query.trim().toLowerCase()
    return list.filter(
      (p) =>
        p.displayName.toLowerCase().includes(q)
        || String(p.processId).includes(q)
        || (p.executablePath?.toLowerCase().includes(q) ?? false),
    )
  }, [snapshot, query])

  const filteredPorts = useMemo(() => {
    if (!portQuery.trim()) return ports
    const q = portQuery.trim().toLowerCase()
    return ports.filter(
      (p) =>
        p.processName.toLowerCase().includes(q)
        || String(p.processId).includes(q)
        || String(p.localPort).includes(q)
        || p.localAddress.includes(q),
    )
  }, [ports, portQuery])

  const selected = filtered.find((p) => p.processId === selectedPid) ?? null

  return (
    <div className="flex h-full min-h-[480px] flex-col gap-3">
      <div className="flex flex-wrap items-end justify-between gap-2">
        <div>
          <h1 className="text-xl font-semibold">网络监控</h1>
          <p className="mt-1 text-sm text-gray-500">连接流量 · 端口管理 · 一键测速</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Button
            variant="outline"
            className="h-8 text-xs"
            onClick={async () => {
              const asAdmin = !snapshot?.isElevated
              if (!window.confirm(asAdmin ? '将以管理员身份重启程序盒子？' : '将以降权普通用户重启？'))
                return
              await api.post('/network/elevation/restart', { asAdmin })
            }}
          >
            {snapshot?.isElevated ? '降权重启' : '管理员重启'}
          </Button>
          <div className="flex gap-1 rounded-md border bg-white p-0.5">
            {([
              ['monitor', '流量'],
              ['ports', '端口'],
              ['speed', '测速'],
            ] as const).map(([id, label]) => (
              <button
                key={id}
                type="button"
                className={cn(
                  'rounded px-3 py-1 text-sm',
                  tab === id ? 'bg-blue-50 font-medium text-blue-700' : 'text-gray-600 hover:bg-gray-50',
                )}
                onClick={() => setTab(id)}
              >
                {label}
              </button>
            ))}
          </div>
        </div>
      </div>

      {tab === 'monitor' && (
        <>
          <div className="flex flex-wrap gap-2">
            <input
              className="min-w-[220px] flex-1 rounded-md border bg-white px-2 py-1.5 text-sm"
              placeholder="搜索进程…"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
            />
            <Button variant="outline" onClick={() => setPaused((v) => !v)}>{paused ? '继续' : '暂停'}</Button>
            <Button variant="outline" onClick={() => void load()}>立即刷新</Button>
          </div>
          {error && <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>}
          <div className="grid min-h-0 flex-1 grid-cols-[1.2fr_1fr] gap-3">
            <div className="overflow-hidden rounded-lg border bg-white">
              <div className="border-b px-3 py-2 text-sm font-semibold">进程列表</div>
              <div className="max-h-[520px] overflow-auto">
                <table className="w-full text-left text-[12px]">
                  <thead className="sticky top-0 bg-gray-50 text-gray-600">
                    <tr>
                      <th className="px-2 py-1.5">进程</th>
                      <th className="px-2 py-1.5">PID</th>
                      <th className="px-2 py-1.5">连接</th>
                      <th className="px-2 py-1.5">下载</th>
                      <th className="px-2 py-1.5">上传</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filtered.map((p) => (
                      <tr
                        key={p.processId}
                        className={cn('cursor-pointer border-t hover:bg-blue-50/60', selectedPid === p.processId && 'bg-blue-50')}
                        onClick={() => setSelectedPid(p.processId)}
                      >
                        <td className="max-w-[140px] truncate px-2 py-1">{p.displayName}</td>
                        <td className="px-2 py-1 tabular-nums">{p.processId}</td>
                        <td className="px-2 py-1 tabular-nums">{p.connectionCount}</td>
                        <td className="px-2 py-1 tabular-nums">{p.downloadRateText}</td>
                        <td className="px-2 py-1 tabular-nums">{p.uploadRateText}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
            <div className="overflow-hidden rounded-lg border bg-white">
              <div className="border-b px-3 py-2 text-sm font-semibold">连接详情</div>
              <div className="px-3 py-2 text-xs text-gray-500">
                {selected ? `${selected.displayName} (PID ${selected.processId})` : '选择进程查看连接'}
              </div>
              <div className="max-h-[480px] overflow-auto">
                <table className="w-full text-left text-[12px]">
                  <thead className="sticky top-0 bg-gray-50 text-gray-600">
                    <tr>
                      <th className="px-2 py-1.5">协议</th>
                      <th className="px-2 py-1.5">本地</th>
                      <th className="px-2 py-1.5">远程</th>
                      <th className="px-2 py-1.5">状态</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(selected?.connections ?? []).map((c, i) => (
                      <tr key={`${c.localEndpoint}-${i}`} className="border-t">
                        <td className="px-2 py-1">{c.protocol}</td>
                        <td className="px-2 py-1">{c.localEndpoint}</td>
                        <td className="px-2 py-1">{c.remoteEndpoint}</td>
                        <td className="px-2 py-1">{c.state}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
          <div className="flex justify-between rounded-lg border bg-white px-3 py-2 text-xs text-gray-600">
            <span>
              连接: {snapshot?.totalConnections ?? '—'}
              {snapshot?.statusHint ? ` | ${snapshot.statusHint}` : ''}
              {snapshot?.isElevated ? ' | 管理员' : ''}
            </span>
            <span className="flex gap-4">
              <span>总下载: <strong className="text-green-700">{snapshot?.totalDownloadText}</strong></span>
              <span>总上传: <strong className="text-amber-700">{snapshot?.totalUploadText}</strong></span>
            </span>
          </div>
        </>
      )}

      {tab === 'ports' && (
        <>
          <div className="flex gap-2">
            <input
              className="min-w-[220px] flex-1 rounded-md border bg-white px-2 py-1.5 text-sm"
              placeholder="搜索端口 / 进程…"
              value={portQuery}
              onChange={(e) => setPortQuery(e.target.value)}
            />
            <Button variant="outline" onClick={() => void loadPorts()}>刷新</Button>
          </div>
          <div className="overflow-hidden rounded-lg border bg-white">
            <div className="max-h-[560px] overflow-auto">
              <table className="w-full text-left text-[12px]">
                <thead className="sticky top-0 bg-gray-50 text-gray-600">
                  <tr>
                    <th className="px-2 py-1.5">协议</th>
                    <th className="px-2 py-1.5">本地</th>
                    <th className="px-2 py-1.5">远程</th>
                    <th className="px-2 py-1.5">状态</th>
                    <th className="px-2 py-1.5">进程</th>
                    <th className="px-2 py-1.5">PID</th>
                    <th className="px-2 py-1.5">操作</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredPorts.map((p, i) => (
                    <tr key={`${p.protocol}-${p.localPort}-${p.processId}-${i}`} className="border-t">
                      <td className="px-2 py-1">{p.protocol}</td>
                      <td className="px-2 py-1">{p.localAddress}:{p.localPort}</td>
                      <td className="px-2 py-1">{p.remoteAddress}:{p.remotePort}</td>
                      <td className="px-2 py-1">{p.state}</td>
                      <td className="max-w-[120px] truncate px-2 py-1">{p.processName}</td>
                      <td className="px-2 py-1 tabular-nums">{p.processId}</td>
                      <td className="px-2 py-1">
                        {p.processId > 0 && (
                          <button
                            type="button"
                            className="text-red-600 hover:underline"
                            onClick={async () => {
                              await api.post(`/network/ports/${p.processId}/kill`)
                              await loadPorts()
                            }}
                          >
                            结束
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </>
      )}

      {tab === 'speed' && (
        <div className="mx-auto w-full max-w-lg space-y-4 rounded-lg border bg-white p-5">
          <div className="flex gap-2">
            <Button
              disabled={speed?.running}
              onClick={async () => {
                await api.post('/network/speedtest/start')
                await loadSpeed()
              }}
            >
              开始测速
            </Button>
            <Button
              variant="outline"
              disabled={!speed?.running}
              onClick={async () => {
                await api.post('/network/speedtest/cancel')
                await loadSpeed()
              }}
            >
              取消
            </Button>
          </div>
          {speed?.running && (
            <div className="space-y-1 text-sm">
              <div>{speed.progress?.message || speed.progress?.phase || '测速中…'}</div>
              <div className="h-2 overflow-hidden rounded bg-gray-100">
                <div
                  className="h-full bg-blue-500 transition-all"
                  style={{ width: `${Math.round((speed.progress?.overallPercent ?? 0) * 100)}%` }}
                />
              </div>
              <div className="text-xs text-gray-500">{speed.progress?.currentSpeedText}</div>
            </div>
          )}
          {speed?.result && (
            <div className="grid grid-cols-2 gap-3 text-sm">
              <div>下载 <strong className="text-green-700">{speed.result.downloadText}</strong></div>
              <div>上传 <strong className="text-amber-700">{speed.result.uploadText}</strong></div>
              <div>延迟 {speed.result.pingText}</div>
              <div>抖动 {speed.result.jitterText}</div>
              <div className="col-span-2 text-xs text-gray-500">
                {speed.result.serverName} · {speed.result.durationText}
                {speed.result.uploadMessage ? ` · ${speed.result.uploadMessage}` : ''}
              </div>
            </div>
          )}
          {speed?.error && <div className="text-sm text-red-600">{speed.error}</div>}
        </div>
      )}
    </div>
  )
}
