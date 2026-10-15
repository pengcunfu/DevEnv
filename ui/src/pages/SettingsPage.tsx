import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, fetchConfig, saveConfig, type AppConfig } from '@/lib/api'
import { Button } from '@/components/ui/button'

type UpdateStatus = {
  isInstalled: boolean
  currentVersion: string
  pendingVersion?: string
  downloading: boolean
  downloadProgress: number
  error?: string
}

type TranslatorSite = { id: string; name: string }

export function SettingsPage() {
  const [config, setConfig] = useState<AppConfig | null>(null)
  const [message, setMessage] = useState('')
  const [saving, setSaving] = useState(false)
  const [update, setUpdate] = useState<UpdateStatus | null>(null)
  const [updateMsg, setUpdateMsg] = useState('')
  const [translatorSites, setTranslatorSites] = useState<TranslatorSite[]>([])
  const [translatorSiteId, setTranslatorSiteId] = useState('youdao')

  useEffect(() => {
    fetchConfig()
      .then(setConfig)
      .catch((err: unknown) => setMessage(err instanceof Error ? err.message : '加载失败'))
    api.get<UpdateStatus>('/update/status').then((r) => setUpdate(r.data)).catch(() => {})
    void (async () => {
      try {
        const [sites, cfg] = await Promise.all([
          api.get<TranslatorSite[]>('/translator/sites'),
          api.get<{ lastWebSiteId: string }>('/translator/config'),
        ])
        setTranslatorSites(sites.data)
        setTranslatorSiteId(cfg.data.lastWebSiteId || sites.data[0]?.id || 'youdao')
      } catch { /* ignore */ }
    })()
  }, [])

  useEffect(() => {
    if (!update?.downloading) return
    const id = window.setInterval(() => {
      api.get<UpdateStatus>('/update/status').then((r) => setUpdate(r.data)).catch(() => {})
    }, 500)
    return () => window.clearInterval(id)
  }, [update?.downloading])

  async function onSave() {
    if (!config) return
    setSaving(true)
    setMessage('')
    try {
      const saved = await saveConfig(config)
      setConfig(saved)
      setMessage('已保存')
    } catch (err) {
      setMessage(err instanceof Error ? err.message : '保存失败')
    } finally {
      setSaving(false)
    }
  }

  if (!config) {
    return <div className="rounded-lg border bg-white p-5 text-sm text-gray-600">{message || '加载中…'}</div>
  }

  return (
    <div className="mx-auto max-w-2xl space-y-4">
      <div className="rounded-lg border border-gray-200 bg-white p-5 shadow-sm">
        <h1 className="text-lg font-semibold">设置</h1>
        <p className="mt-1 text-sm text-gray-500">常规选项（与现版 data.json Config 对齐）</p>

        <div className="mt-4 space-y-3">
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={!!config.TrayIcon}
              onChange={(e) => setConfig({ ...config, TrayIcon: e.target.checked })}
            />
            显示托盘图标
          </label>
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={!!config.SelfStarting}
              onChange={(e) => setConfig({ ...config, SelfStarting: e.target.checked })}
            />
            开机自启动
          </label>
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={!!config.DarkMode}
              onChange={(e) => setConfig({ ...config, DarkMode: e.target.checked })}
            />
            深色模式
          </label>
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={!!config.DoubleClickToLaunch}
              onChange={(e) => setConfig({ ...config, DoubleClickToLaunch: e.target.checked })}
            />
            双击启动快捷方式
          </label>
          <label className="block text-sm">
            <span className="text-gray-600">环境根目录</span>
            <input
              className="mt-1 w-full rounded-md border border-gray-200 px-2 py-1.5"
              value={config.BaseEnvPath ?? ''}
              onChange={(e) => setConfig({ ...config, BaseEnvPath: e.target.value })}
            />
          </label>
          <label className="block text-sm">
            <span className="text-gray-600">显示窗口快捷键</span>
            <input
              className="mt-1 w-full rounded-md border border-gray-200 px-2 py-1.5"
              value={config.ShowWindowHotkey ?? ''}
              onChange={(e) => setConfig({ ...config, ShowWindowHotkey: e.target.value })}
              placeholder="Ctrl+Alt+D"
            />
          </label>
          <p className="text-xs text-gray-500">
            全局热键：显示窗口（上）· 翻译 Alt+Space · 剪贴板 Ctrl+Space
          </p>
          <label className="block text-sm">
            <span className="text-gray-600">默认翻译引擎</span>
            <select
              className="mt-1 w-full rounded-md border border-gray-200 px-2 py-1.5 bg-white"
              value={translatorSiteId}
              onChange={async (e) => {
                const id = e.target.value
                setTranslatorSiteId(id)
                await api.put('/translator/site', { id, open: false })
                setMessage(`默认翻译引擎已设为 ${translatorSites.find((s) => s.id === id)?.name ?? id}`)
              }}
            >
              {translatorSites.map((s) => (
                <option key={s.id} value={s.id}>{s.name}</option>
              ))}
            </select>
          </label>
        </div>

        <div className="mt-5 flex items-center gap-3">
          <Button onClick={onSave} disabled={saving}>
            {saving ? '保存中…' : '保存'}
          </Button>
          <Link to="/tools/startup" className="text-sm text-blue-600 hover:underline">启动项管理</Link>
          {message && <span className="text-sm text-gray-500">{message}</span>}
        </div>
      </div>

      <div className="rounded-lg border border-gray-200 bg-white p-5 shadow-sm">
        <h2 className="text-base font-semibold">检查更新</h2>
        <p className="mt-1 text-sm text-gray-500">
          当前版本 {update?.currentVersion ?? '…'}
          {update && !update.isInstalled ? '（开发态，不可 Velopack 更新）' : ''}
        </p>
        <div className="mt-3 flex flex-wrap gap-2">
          <Button
            variant="outline"
            onClick={async () => {
              try {
                const { data } = await api.post<{ updateAvailable: boolean; message?: string; version?: string }>('/update/check')
                setUpdateMsg(data.message || (data.updateAvailable ? `新版本 ${data.version}` : '已是最新'))
                const st = await api.get<UpdateStatus>('/update/status')
                setUpdate(st.data)
              } catch (err) {
                const data = (err as { response?: { data?: { error?: string } } })?.response?.data
                setUpdateMsg(data?.error || (err instanceof Error ? err.message : '检查失败'))
              }
            }}
          >
            检查更新
          </Button>
          {update?.pendingVersion && !update.downloading && (
            <Button
              onClick={async () => {
                await api.post('/update/download')
                setUpdateMsg('开始下载…')
                const st = await api.get<UpdateStatus>('/update/status')
                setUpdate(st.data)
              }}
            >
              下载 {update.pendingVersion}
            </Button>
          )}
          {update?.pendingVersion && update.downloadProgress >= 100 && (
            <Button
              onClick={async () => {
                await api.post('/update/apply')
                setUpdateMsg('正在应用更新并重启…')
              }}
            >
              重启并应用
            </Button>
          )}
        </div>
        {update?.downloading && (
          <div className="mt-3">
            <div className="h-2 overflow-hidden rounded bg-gray-100">
              <div className="h-full bg-blue-500" style={{ width: `${update.downloadProgress}%` }} />
            </div>
            <div className="mt-1 text-xs text-gray-500">{update.downloadProgress}%</div>
          </div>
        )}
        {(updateMsg || update?.error) && (
          <div className="mt-2 text-sm text-gray-600">{updateMsg || update?.error}</div>
        )}
      </div>
    </div>
  )
}
