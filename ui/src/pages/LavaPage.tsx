import { useCallback, useEffect, useMemo, useState } from 'react'
import { isAxiosError } from 'axios'
import { api } from '@/lib/api'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

type Proc = {
  name: string
  displayName: string
  description: string
  status: string
  statusText: string
  statusColor?: string
  processId?: number | null
  canStart: boolean
  canStop: boolean
  needsInstall: boolean
}

type Download = {
  id: string
  softwareName: string
  version: string
  fileName: string
  status: string
  statusText: string
  progress: number
  speedText?: string
  sizeText?: string
  errorMessage?: string
  canOpen?: boolean
  isActive?: boolean
}

type DownloadStats = {
  total: number
  completed: number
  failed: number
  downloading: number
  totalBytesText: string
}

type CatalogVersion = { version: string; url: string; fileName: string }
type CatalogItem = { name: string; description: string; versions: CatalogVersion[] }
type CatalogCategory = { category: string; items: CatalogItem[] }

type Mirror = { key: string; name: string; url: string; responseTimeMs?: number | null }
type MirrorTool = { key: string; label: string }
type MirrorsResp = {
  tool: string
  tools: MirrorTool[]
  current?: string
  mirrors: Mirror[]
}

type ScanItem = {
  type: string
  name: string
  version: string
  path: string
  isInPath: boolean
}

type DataInfo = {
  root: string
  configFile: string
  cacheDir: string
  appsDir: string
  historyFile: string
  settings: {
    autoExtractPortable: boolean
    maxRetries: number
    timeout: number
    chunkSizeMb: number
    useSystemProxy: boolean
    customProxy: string
  }
  about: {
    productName: string
    productShortName: string
    tagline: string
    version: string
    docsUrl: string
    repositoryUrl: string
  }
}

type Tab = 'services' | 'downloads' | 'active' | 'history' | 'mirrors' | 'scan' | 'data'

function apiError(err: unknown, fallback: string) {
  if (isAxiosError(err)) {
    const data = err.response?.data as { error?: string; message?: string } | undefined
    return data?.error || data?.message || err.message || fallback
  }
  return err instanceof Error ? err.message : fallback
}

function statusDotClass(color?: string, status?: string) {
  const c = (color || '').toLowerCase()
  if (c.includes('green') || status === 'Running') return 'bg-emerald-500'
  if (c.includes('blue') || status === 'ExternalRunning') return 'bg-sky-500'
  if (c.includes('orange') || c.includes('amber') || status === 'NotConfigured') return 'bg-amber-500'
  if (c.includes('red')) return 'bg-red-500'
  return 'bg-gray-400'
}

export function LavaPage() {
  const [tab, setTab] = useState<Tab>('services')
  const [items, setItems] = useState<Proc[]>([])
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState<string | null>(null)
  const [downloads, setDownloads] = useState<Download[]>([])
  const [downloadStats, setDownloadStats] = useState<DownloadStats | null>(null)
  const [catalog, setCatalog] = useState<CatalogCategory[]>([])
  const [catalogQuery, setCatalogQuery] = useState('')
  const [catalogCategory, setCatalogCategory] = useState('全部')
  const [selectedVersion, setSelectedVersion] = useState<Record<string, string>>({})
  const [downloadFilter, setDownloadFilter] = useState('')
  const [mirrors, setMirrors] = useState<MirrorsResp | null>(null)
  const [tool, setTool] = useState('Pip')
  const [testingMirrors, setTestingMirrors] = useState(false)
  const [scan, setScan] = useState<ScanItem[]>([])
  const [scanning, setScanning] = useState(false)
  const [dataInfo, setDataInfo] = useState<DataInfo | null>(null)
  const [toolsOpen, setToolsOpen] = useState(false)

  const load = useCallback(async () => {
    const { data } = await api.get<Proc[]>('/lava/processes')
    setItems(data)
  }, [])

  const loadDownloads = useCallback(async () => {
    const [{ data: dl }, { data: cat }] = await Promise.all([
      api.get<{ items: Download[]; stats: DownloadStats }>('/lava/downloads'),
      api.get<{ categories: CatalogCategory[] }>('/lava/software-catalog'),
    ])
    setDownloads(dl.items ?? [])
    setDownloadStats(dl.stats ?? null)
    setCatalog(cat.categories ?? [])
  }, [])

  const loadMirrors = useCallback(async () => {
    const { data } = await api.get<MirrorsResp>('/lava/mirrors', { params: { tool } })
    setMirrors(data)
  }, [tool])

  const loadDataInfo = useCallback(async () => {
    const { data } = await api.get<DataInfo>('/lava/data-info')
    setDataInfo(data)
  }, [])

  useEffect(() => {
    void load()
    void loadDataInfo()
    const id = window.setInterval(() => void load(), 2000)
    return () => window.clearInterval(id)
  }, [load, loadDataInfo])

  useEffect(() => {
    if (tab !== 'downloads' && tab !== 'active' && tab !== 'history') return
    void loadDownloads()
    const id = window.setInterval(() => void loadDownloads(), 1500)
    return () => window.clearInterval(id)
  }, [tab, loadDownloads])

  useEffect(() => {
    if (tab !== 'mirrors') return
    void loadMirrors()
  }, [tab, loadMirrors])

  useEffect(() => {
    if (tab !== 'data') return
    void loadDataInfo()
  }, [tab, loadDataInfo])

  useEffect(() => {
    const completed = downloads.filter((d) => d.status === 'Completed')
    if (completed.length === 0) return
    const key = `lava-seen-${completed.map((d) => d.id).join(',')}`
    if (sessionStorage.getItem(key)) return
    const newest = completed[0]
    if (newest) {
      setMessage(`下载完成：${newest.softwareName} ${newest.version}`)
      sessionStorage.setItem(key, '1')
    }
  }, [downloads])

  const categories = useMemo(
    () => ['全部', ...catalog.map((c) => c.category)],
    [catalog],
  )

  const filteredCatalog = useMemo(() => {
    const q = catalogQuery.trim().toLowerCase()
    return catalog
      .filter((c) => catalogCategory === '全部' || c.category === catalogCategory)
      .map((c) => ({
        ...c,
        items: c.items.filter((item) => {
          if (!q) return true
          return (
            item.name.toLowerCase().includes(q)
            || item.description.toLowerCase().includes(q)
          )
        }),
      }))
      .filter((c) => c.items.length > 0)
  }, [catalog, catalogCategory, catalogQuery])

  const activeDownloads = downloads.filter((d) => d.isActive)
  const historyDownloads = downloads

  const scanSummary = useMemo(() => {
    const map = new Map<string, number>()
    for (const s of scan) {
      map.set(s.type, (map.get(s.type) || 0) + 1)
    }
    return [...map.entries()]
  }, [scan])

  const goDownload = (serviceName?: string) => {
    setTab('downloads')
    if (serviceName) {
      setCatalogQuery(serviceName)
      setDownloadFilter(serviceName)
    }
  }

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <h1 className="text-lg font-semibold">熔岩</h1>
          <p className="text-sm text-gray-500">
            {dataInfo?.about.tagline
              || '开源智能开发环境管理工具，为多语言与全栈开发者提供一键下载、配置与管理'}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <div className="flex gap-1 rounded-md border bg-white p-0.5">
            {([
              ['services', '服务'],
              ['downloads', '软件下载'],
              ['active', '活动下载'],
              ['history', '下载历史'],
              ['mirrors', '镜像'],
              ['scan', '扫描'],
              ['data', '数据目录'],
            ] as const).map(([id, label]) => (
              <button
                key={id}
                type="button"
                className={cn(
                  'rounded px-3 py-1 text-sm',
                  tab === id ? 'bg-blue-50 font-medium text-blue-700' : 'text-gray-600 hover:bg-gray-50',
                )}
                onClick={() => { setTab(id); setToolsOpen(false) }}
              >
                {label}
              </button>
            ))}
          </div>
          <div className="relative">
            <Button variant="outline" className="h-8" onClick={() => setToolsOpen((v) => !v)}>
              工具 ▾
            </Button>
            {toolsOpen && (
              <div className="absolute right-0 z-20 mt-1 w-44 rounded-md border bg-white py-1 shadow-md">
                <button
                  type="button"
                  className="block w-full px-3 py-1.5 text-left text-sm hover:bg-gray-50"
                  onClick={async () => {
                    setToolsOpen(false)
                    await api.post('/lava/open-environment-variables')
                  }}
                >
                  环境变量
                </button>
                <button
                  type="button"
                  className="block w-full px-3 py-1.5 text-left text-sm hover:bg-gray-50"
                  onClick={() => { setToolsOpen(false); setTab('data') }}
                >
                  数据目录
                </button>
                <button
                  type="button"
                  className="block w-full px-3 py-1.5 text-left text-sm hover:bg-gray-50"
                  onClick={async () => {
                    setToolsOpen(false)
                    await api.post('/lava/open-apps-dir')
                  }}
                >
                  应用目录
                </button>
                <a
                  className="block w-full px-3 py-1.5 text-left text-sm hover:bg-gray-50"
                  href={dataInfo?.about.docsUrl || 'https://docs.devenv.cn/guide/lava/getting-started'}
                  target="_blank"
                  rel="noreferrer"
                  onClick={() => setToolsOpen(false)}
                >
                  官方文档
                </a>
              </div>
            )}
          </div>
        </div>
      </div>
      {message && <div className="text-sm text-gray-600">{message}</div>}

      {tab === 'services' && (
        <>
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" onClick={() => api.post('/lava/open-apps-dir')}>应用目录</Button>
            <Button variant="outline" onClick={() => setTab('scan')}>环境扫描</Button>
            <Button variant="outline" onClick={() => setTab('downloads')}>软件下载</Button>
            <Button variant="outline" onClick={() => void load()}>刷新</Button>
          </div>
          <div className="overflow-hidden rounded-lg border bg-white">
            <table className="w-full text-left text-sm">
              <thead className="bg-gray-50 text-gray-600">
                <tr>
                  <th className="px-3 py-2">服务</th>
                  <th className="px-3 py-2">状态</th>
                  <th className="px-3 py-2 w-52">操作</th>
                </tr>
              </thead>
              <tbody>
                {items.map((p) => (
                  <tr key={p.name} className="border-t">
                    <td className="px-3 py-2">
                      <div className="font-medium">{p.displayName}</div>
                      <div className="text-xs text-gray-500">{p.description}</div>
                    </td>
                    <td className="px-3 py-2">
                      <div className="flex items-center gap-2">
                        <span className={cn('inline-block h-2 w-2 rounded-full', statusDotClass(p.statusColor, p.status))} />
                        <span>{p.statusText}</span>
                        {p.processId ? (
                          <span className="text-xs text-gray-400">PID {p.processId}</span>
                        ) : null}
                      </div>
                    </td>
                    <td className="px-3 py-2">
                      <div className="flex flex-wrap gap-2">
                        <Button
                          className="h-7 text-xs"
                          disabled={!p.canStart || busy === p.name}
                          onClick={async () => {
                            setBusy(p.name)
                            try {
                              const { data } = await api.post<{ message: string }>(`/lava/processes/${p.name}/start`)
                              setMessage(data.message)
                              await load()
                            } catch (err) {
                              setMessage(apiError(err, '启动失败'))
                            } finally {
                              setBusy(null)
                            }
                          }}
                        >
                          {busy === p.name ? '启动中…' : '启动'}
                        </Button>
                        <Button
                          variant="outline"
                          className="h-7 text-xs"
                          disabled={!p.canStop || busy === p.name}
                          onClick={async () => {
                            setBusy(p.name)
                            try {
                              const { data } = await api.post<{ message: string }>(`/lava/processes/${p.name}/stop`)
                              setMessage(data.message)
                              await load()
                            } catch (err) {
                              setMessage(apiError(err, '停止失败'))
                            } finally {
                              setBusy(null)
                            }
                          }}
                        >
                          停止
                        </Button>
                        {p.needsInstall && (
                          <Button
                            variant="outline"
                            className="h-7 text-xs text-amber-700"
                            onClick={() => goDownload(p.displayName || p.name)}
                          >
                            去下载
                          </Button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}

      {tab === 'downloads' && (
        <div className="space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <input
              className="h-8 w-48 rounded-md border px-2 text-sm"
              placeholder="搜索软件…"
              value={catalogQuery}
              onChange={(e) => setCatalogQuery(e.target.value)}
            />
            <select
              className="h-8 rounded-md border bg-white px-2 text-sm"
              value={catalogCategory}
              onChange={(e) => setCatalogCategory(e.target.value)}
            >
              {categories.map((c) => <option key={c} value={c}>{c}</option>)}
            </select>
            <Button variant="outline" className="h-8" onClick={() => { setCatalogQuery(''); setCatalogCategory('全部') }}>
              重置
            </Button>
            <Button variant="outline" className="h-8" onClick={() => setTab('active')}>
              活动下载 ({activeDownloads.length})
            </Button>
            <Button variant="outline" className="h-8" onClick={() => setTab('history')}>
              下载历史
            </Button>
            {downloadStats && (
              <span className="text-xs text-gray-500">
                活动 {downloadStats.downloading} · 完成 {downloadStats.completed} · 失败 {downloadStats.failed}
              </span>
            )}
          </div>

          <div className="overflow-hidden rounded-lg border bg-white">
            <div className="border-b px-3 py-2 text-sm font-semibold">软件目录</div>
            <div className="max-h-[560px] overflow-auto p-2 space-y-3">
              {filteredCatalog.map((cat) => (
                <div key={cat.category}>
                  <div className="mb-1 text-xs font-semibold text-gray-500">{cat.category}</div>
                  {cat.items.map((item) => {
                    const versions = item.versions
                    const selected = selectedVersion[item.name] || versions[0]?.version || ''
                    const ver = versions.find((v) => v.version === selected) || versions[0]
                    return (
                      <div key={item.name} className="mb-2 rounded border p-2">
                        <div className="text-sm font-medium">{item.name}</div>
                        <div className="text-xs text-gray-500">{item.description}</div>
                        <div className="mt-2 flex flex-wrap items-center gap-2">
                          <select
                            className="h-7 rounded border bg-white px-1 text-xs"
                            value={selected}
                            onChange={(e) => setSelectedVersion((m) => ({ ...m, [item.name]: e.target.value }))}
                          >
                            {versions.map((v) => (
                              <option key={v.version} value={v.version}>{v.version || '默认'}</option>
                            ))}
                          </select>
                          <Button
                            className="h-7 text-xs"
                            disabled={!ver}
                            onClick={async () => {
                              if (!ver) return
                              try {
                                await api.post('/lava/downloads/start', {
                                  url: ver.url,
                                  fileName: ver.fileName,
                                  softwareName: item.name,
                                  version: ver.version,
                                })
                                setMessage(`已开始下载 ${item.name} ${ver.version}`)
                                setTab('active')
                                await loadDownloads()
                              } catch (err) {
                                setMessage(apiError(err, '开始下载失败'))
                              }
                            }}
                          >
                            下载
                          </Button>
                        </div>
                      </div>
                    )
                  })}
                </div>
              ))}
              {filteredCatalog.length === 0 && (
                <div className="py-8 text-center text-sm text-gray-500">无匹配软件</div>
              )}
            </div>
          </div>
        </div>
      )}

      {tab === 'active' && (
        <div className="space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <Button variant="outline" className="h-8" onClick={() => void loadDownloads()}>刷新</Button>
            <Button variant="outline" className="h-8" onClick={() => api.post('/lava/downloads/open-cache-dir')}>
              打开缓存
            </Button>
            <Button variant="outline" className="h-8" onClick={() => setTab('downloads')}>去下载软件</Button>
            {downloadStats && (
              <span className="text-xs text-gray-500">
                下载中 {downloadStats.downloading} · 活动任务 {activeDownloads.length}
              </span>
            )}
          </div>
          <div className="overflow-hidden rounded-lg border bg-white">
            <div className="border-b px-3 py-2 text-sm font-semibold">活动下载 ({activeDownloads.length})</div>
            <DownloadTable
              rows={activeDownloads}
              tall
              onCancel={async (id) => {
                await api.post(`/lava/downloads/${id}/cancel`)
                await loadDownloads()
              }}
            />
          </div>
        </div>
      )}

      {tab === 'history' && (
        <div className="space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <Button variant="outline" className="h-8" onClick={() => void loadDownloads()}>刷新</Button>
            <Button variant="outline" className="h-8" onClick={() => api.post('/lava/downloads/open-cache-dir')}>
              打开缓存
            </Button>
            <Button variant="outline" className="h-8" onClick={() => api.post('/lava/open-apps-dir')}>
              应用目录
            </Button>
            <Button
              variant="outline"
              className="h-8"
              onClick={async () => {
                await api.post('/lava/downloads/clear-completed')
                await loadDownloads()
              }}
            >
              清除已完成
            </Button>
            <Button
              variant="outline"
              className="h-8"
              onClick={async () => {
                await api.post('/lava/downloads/clear-failed')
                await loadDownloads()
              }}
            >
              清除失败
            </Button>
            <Button
              variant="outline"
              className="h-8"
              onClick={async () => {
                if (!window.confirm('确定清空全部下载历史？')) return
                await api.post('/lava/downloads/clear-all')
                await loadDownloads()
              }}
            >
              清空全部
            </Button>
            {downloadStats && (
              <span className="text-xs text-gray-500">
                共 {downloadStats.total} · 完成 {downloadStats.completed} · 失败 {downloadStats.failed} · 总量 {downloadStats.totalBytesText}
              </span>
            )}
          </div>
          {downloadFilter && (
            <div className="text-xs text-amber-700">
              筛选：{downloadFilter}
              <button type="button" className="ml-2 underline" onClick={() => setDownloadFilter('')}>清除</button>
            </div>
          )}
          <div className="overflow-hidden rounded-lg border bg-white">
            <div className="border-b px-3 py-2 text-sm font-semibold">下载历史</div>
            <DownloadTable
              rows={historyDownloads.filter((d) =>
                !downloadFilter
                || d.softwareName.toLowerCase().includes(downloadFilter.toLowerCase()),
              )}
              tall
              showOpen
              onOpen={async (id) => {
                try {
                  await api.post(`/lava/downloads/${id}/open-file`)
                } catch (err) {
                  setMessage(apiError(err, '打开文件失败'))
                }
              }}
              onCancel={async (id) => {
                await api.post(`/lava/downloads/${id}/cancel`)
                await loadDownloads()
              }}
            />
          </div>
        </div>
      )}

      {tab === 'mirrors' && mirrors && (
        <div className="space-y-3">
          <div className="flex flex-wrap gap-2">
            <select
              className="rounded-md border bg-white px-2 py-1.5 text-sm"
              value={tool}
              onChange={(e) => setTool(e.target.value)}
            >
              {mirrors.tools.map((t) => <option key={t.key} value={t.key}>{t.label}</option>)}
            </select>
            <Button variant="outline" onClick={() => void loadMirrors()}>刷新</Button>
            <Button
              variant="outline"
              disabled={testingMirrors}
              onClick={async () => {
                setTestingMirrors(true)
                try {
                  const { data } = await api.post<{
                    mirrors: Mirror[]
                    recommended?: { name: string; responseTimeMs?: number }
                  }>('/lava/mirrors/test', { tool })
                  setMirrors((prev) => prev ? { ...prev, mirrors: data.mirrors } : prev)
                  setMessage(
                    data.recommended
                      ? `测速完成，推荐：${data.recommended.name} (${Math.round(data.recommended.responseTimeMs || 0)} ms)`
                      : '测速完成，所有镜像均不可用',
                  )
                } catch (err) {
                  setMessage(apiError(err, '测速失败'))
                } finally {
                  setTestingMirrors(false)
                }
              }}
            >
              {testingMirrors ? '测速中…' : '测试速度'}
            </Button>
            {mirrors.current && (
              <span className="self-center text-sm text-gray-500">当前：{mirrors.current}</span>
            )}
          </div>
          <div className="grid gap-2 sm:grid-cols-2">
            {mirrors.mirrors.map((m) => (
              <div key={m.key} className="rounded-lg border bg-white p-3">
                <div className="flex items-start justify-between gap-2">
                  <div className="font-medium text-sm">{m.name}</div>
                  {m.responseTimeMs != null && (
                    <span className="text-xs text-emerald-700">{Math.round(m.responseTimeMs)} ms</span>
                  )}
                  {m.responseTimeMs == null && testingMirrors === false && mirrors.mirrors.some((x) => x.responseTimeMs != null) && (
                    <span className="text-xs text-gray-400">超时</span>
                  )}
                </div>
                <div className="truncate text-xs text-gray-500">{m.url}</div>
                <Button
                  className="mt-2 h-7 text-xs"
                  variant="outline"
                  onClick={async () => {
                    try {
                      const { data } = await api.post<{ message?: string }>('/lava/mirrors/configure', {
                        tool,
                        key: m.key,
                      })
                      setMessage(data.message || `已切换到 ${m.name}`)
                      await loadMirrors()
                    } catch (err) {
                      setMessage(apiError(err, '配置失败'))
                    }
                  }}
                >
                  使用此镜像
                </Button>
              </div>
            ))}
          </div>
        </div>
      )}

      {tab === 'scan' && (
        <div className="space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <Button
              disabled={scanning}
              onClick={async () => {
                setScanning(true)
                try {
                  const { data } = await api.get<ScanItem[]>('/lava/scan')
                  setScan(data)
                  setMessage(`扫描完成：${data.length} 项`)
                } catch (err) {
                  setMessage(apiError(err, '扫描失败'))
                } finally {
                  setScanning(false)
                }
              }}
            >
              {scanning ? '扫描中…' : '开始扫描'}
            </Button>
            {scanSummary.length > 0 && (
              <div className="flex flex-wrap gap-2 text-xs text-gray-600">
                {scanSummary.map(([type, count]) => (
                  <span key={type} className="rounded bg-gray-100 px-2 py-0.5">{type}: {count}</span>
                ))}
              </div>
            )}
          </div>
          <div className="overflow-hidden rounded-lg border bg-white">
            <table className="w-full text-left text-sm">
              <thead className="bg-gray-50 text-gray-600">
                <tr>
                  <th className="px-3 py-2">类型</th>
                  <th className="px-3 py-2">名称</th>
                  <th className="px-3 py-2">版本</th>
                  <th className="px-3 py-2">路径</th>
                  <th className="px-3 py-2">PATH</th>
                </tr>
              </thead>
              <tbody>
                {scan.map((s, i) => (
                  <tr key={`${s.type}-${s.name}-${i}`} className="border-t">
                    <td className="px-3 py-2">{s.type}</td>
                    <td className="px-3 py-2">{s.name}</td>
                    <td className="px-3 py-2 text-xs">{s.version}</td>
                    <td className="max-w-[280px] truncate px-3 py-2 font-mono text-xs">{s.path}</td>
                    <td className="px-3 py-2 text-xs">{s.isInPath ? '是' : '否'}</td>
                  </tr>
                ))}
                {scan.length === 0 && (
                  <tr>
                    <td colSpan={5} className="px-3 py-6 text-center text-sm text-gray-500">
                      点击「开始扫描」检测本机已安装环境
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {tab === 'data' && (
        <div className="space-y-3">
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" onClick={() => api.post('/lava/open-data-root')}>打开数据目录</Button>
            <Button variant="outline" onClick={() => api.post('/lava/open-config')}>打开 config.json</Button>
            <Button variant="outline" onClick={() => api.post('/lava/open-apps-dir')}>打开应用目录</Button>
            <Button variant="outline" onClick={() => void loadDataInfo()}>刷新</Button>
          </div>
          {dataInfo ? (
            <>
              <div className="rounded-lg border bg-white p-4 text-sm space-y-1 font-mono">
                <div>根目录: {dataInfo.root}</div>
                <div>运行时配置: {dataInfo.configFile}</div>
                <div>下载缓存: {dataInfo.cacheDir}</div>
                <div>应用目录: {dataInfo.appsDir}</div>
                <div>下载历史: {dataInfo.historyFile}</div>
              </div>
              <div className="rounded-lg border bg-white p-4 text-sm space-y-1">
                <div className="font-semibold">当前配置</div>
                <div>自动解压: {dataInfo.settings.autoExtractPortable ? '是' : '否'}</div>
                <div>最大重试: {dataInfo.settings.maxRetries} 次</div>
                <div>连接超时: {dataInfo.settings.timeout} 秒</div>
                <div>下载块大小: {dataInfo.settings.chunkSizeMb} MB</div>
                <div>系统代理: {dataInfo.settings.useSystemProxy ? '启用' : '禁用'}</div>
                <div>自定义代理: {dataInfo.settings.customProxy || '无'}</div>
              </div>
              <div className="rounded-lg border bg-white p-4 text-sm space-y-1">
                <div className="font-semibold">{dataInfo.about.productName}</div>
                <div className="text-gray-500">{dataInfo.about.tagline}</div>
                <div>版本: {dataInfo.about.version}</div>
                <div className="flex gap-3 pt-1">
                  <a className="text-blue-600 hover:underline" href={dataInfo.about.docsUrl} target="_blank" rel="noreferrer">
                    官方文档
                  </a>
                  <a className="text-blue-600 hover:underline" href={dataInfo.about.repositoryUrl} target="_blank" rel="noreferrer">
                    GitHub
                  </a>
                </div>
              </div>
            </>
          ) : (
            <div className="text-sm text-gray-500">加载中…</div>
          )}
        </div>
      )}
    </div>
  )
}

function DownloadTable({
  rows,
  showOpen,
  tall,
  onOpen,
  onCancel,
}: {
  rows: Download[]
  showOpen?: boolean
  tall?: boolean
  onOpen?: (id: string) => void | Promise<void>
  onCancel: (id: string) => void | Promise<void>
}) {
  return (
    <div className={cn('overflow-auto', tall ? 'max-h-[560px]' : 'max-h-[280px]')}>
      <table className="w-full text-left text-sm">
        <thead className="sticky top-0 bg-gray-50 text-gray-600">
          <tr>
            <th className="px-2 py-1.5">软件</th>
            <th className="px-2 py-1.5">进度</th>
            <th className="px-2 py-1.5">速度</th>
            <th className="px-2 py-1.5">状态</th>
            <th className="px-2 py-1.5">操作</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((d) => (
            <tr key={d.id} className="border-t">
              <td className="px-2 py-1">
                <div>{d.softwareName}</div>
                <div className="text-xs text-gray-500">{d.version} · {d.fileName}</div>
                {d.sizeText && <div className="text-[11px] text-gray-400">{d.sizeText}</div>}
              </td>
              <td className="px-2 py-1 tabular-nums">{d.progress}%</td>
              <td className="px-2 py-1 text-xs">{d.speedText || '-'}</td>
              <td className="px-2 py-1 text-xs">
                {d.statusText || d.status}
                {d.errorMessage ? `: ${d.errorMessage}` : ''}
              </td>
              <td className="px-2 py-1">
                <div className="flex gap-2">
                  {(d.status === 'Downloading' || d.status === 'Pending') && (
                    <button
                      type="button"
                      className="text-xs text-red-600 hover:underline"
                      onClick={() => void onCancel(d.id)}
                    >
                      取消
                    </button>
                  )}
                  {showOpen && d.canOpen && (
                    <button
                      type="button"
                      className="text-xs text-blue-600 hover:underline"
                      onClick={() => void onOpen?.(d.id)}
                    >
                      打开
                    </button>
                  )}
                </div>
              </td>
            </tr>
          ))}
          {rows.length === 0 && (
            <tr>
              <td colSpan={5} className="px-3 py-6 text-center text-sm text-gray-500">暂无记录</td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  )
}
