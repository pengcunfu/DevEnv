import { useCallback, useEffect, useState } from 'react'
import { api, fetchConfig, type AppConfig } from '@/lib/api'
import { Button } from '@/components/ui/button'

type Service = {
  key: string
  name: string
  description: string
  installPath: string
  dataPath: string
  running: boolean
  installed: boolean
  statusText: string
  canStart: boolean
  canStop: boolean
}

export function DevEnvPage() {
  const [config, setConfig] = useState<AppConfig | null>(null)
  const [services, setServices] = useState<Service[]>([])
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState<string | null>(null)

  const load = useCallback(async () => {
    const { data } = await api.get<Service[]>('/devenv/services')
    setServices(data)
  }, [])

  useEffect(() => {
    fetchConfig().then(setConfig).catch(() => undefined)
    void load().catch((err: unknown) => setMessage(err instanceof Error ? err.message : '加载失败'))
    const id = window.setInterval(() => void load(), 2000)
    return () => window.clearInterval(id)
  }, [load])

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <h1 className="text-lg font-semibold">开发环境</h1>
          <p className="text-sm text-gray-500">
            本机 MySQL / Redis / PostgreSQL 进程启停。绿色版请用「熔岩环境」。
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => { window.location.hash = '#/lava' }}>熔岩环境</Button>
          <Button variant="outline" onClick={() => void load()}>刷新</Button>
        </div>
      </div>

      <div className="rounded-lg border bg-white px-4 py-3 text-sm">
        <div className="text-gray-500">环境根目录 (BaseEnvPath)</div>
        <div className="font-mono text-sm">{config?.BaseEnvPath || '—'}</div>
      </div>

      {message && <div className="text-sm text-gray-600">{message}</div>}

      <div className="overflow-hidden rounded-lg border bg-white">
        <table className="w-full text-left text-sm">
          <thead className="bg-gray-50 text-gray-600">
            <tr>
              <th className="px-3 py-2">服务</th>
              <th className="px-3 py-2">状态</th>
              <th className="px-3 py-2 w-56">操作</th>
            </tr>
          </thead>
          <tbody>
            {services.map((s) => (
              <tr key={s.key} className="border-t">
                <td className="px-3 py-2">
                  <div className="font-medium">{s.name}</div>
                  <div className="text-xs text-gray-500">{s.description}</div>
                  <div className="mt-0.5 truncate font-mono text-[11px] text-gray-400">{s.installPath}</div>
                </td>
                <td className="px-3 py-2">
                  <span className={s.running ? 'text-green-700' : s.installed ? 'text-gray-600' : 'text-amber-600'}>
                    {s.statusText}
                  </span>
                </td>
                <td className="px-3 py-2">
                  <div className="flex flex-wrap gap-2">
                    <Button
                      className="h-7 text-xs"
                      disabled={!s.canStart || busy === s.key}
                      onClick={async () => {
                        setBusy(s.key)
                        try {
                          const { data } = await api.post<{ message: string }>(`/devenv/services/${s.key}/start`)
                          setMessage(data.message)
                          await load()
                        } catch (err) {
                          const data = (err as { response?: { data?: { error?: string } } })?.response?.data
                          setMessage(data?.error || (err instanceof Error ? err.message : '启动失败'))
                        } finally {
                          setBusy(null)
                        }
                      }}
                    >
                      启动
                    </Button>
                    <Button
                      variant="outline"
                      className="h-7 text-xs"
                      disabled={!s.canStop || busy === s.key}
                      onClick={async () => {
                        setBusy(s.key)
                        try {
                          const { data } = await api.post<{ message: string }>(`/devenv/services/${s.key}/stop`)
                          setMessage(data.message)
                          await load()
                        } catch (err) {
                          const data = (err as { response?: { data?: { error?: string } } })?.response?.data
                          setMessage(data?.error || (err instanceof Error ? err.message : '停止失败'))
                        } finally {
                          setBusy(null)
                        }
                      }}
                    >
                      停止
                    </Button>
                    <Button
                      variant="outline"
                      className="h-7 text-xs"
                      onClick={() => api.post(`/devenv/services/${s.key}/open-data`)}
                    >
                      数据目录
                    </Button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
