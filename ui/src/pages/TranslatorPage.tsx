import { useEffect, useState } from 'react'
import { api } from '@/lib/api'
import { Button } from '@/components/ui/button'

type Site = { id: string; name: string; homeUrl: string }

/**
 * 熔岩翻译入口页：翻译站在独立宿主窗口（独立 WebView2）中打开，
 * 本页只负责唤起窗口 / 切换默认引擎 / 填充剪贴板。
 */
export function TranslatorPage() {
  const [sites, setSites] = useState<Site[]>([])
  const [current, setCurrent] = useState('youdao')
  const [status, setStatus] = useState('')

  useEffect(() => {
    let cancelled = false
    void (async () => {
      try {
        const [sitesRes, cfgRes] = await Promise.all([
          api.get<Site[]>('/translator/sites'),
          api.get<{ lastWebSiteId: string }>('/translator/config'),
        ])
        if (cancelled) return
        setSites(sitesRes.data)
        const id = cfgRes.data.lastWebSiteId || sitesRes.data[0]?.id || 'youdao'
        setCurrent(id)
        await api.post('/shell/show-translator', { visible: true, siteId: id })
        setStatus('已打开独立翻译窗口（Alt+Space 亦可唤起）')
      } catch (err) {
        if (!cancelled)
          setStatus(err instanceof Error ? err.message : '打开失败')
      }
    })()
    return () => {
      cancelled = true
    }
  }, [])

  async function openWindow(opts?: { siteId?: string; fillClipboard?: boolean }) {
    const siteId = opts?.siteId ?? current
    setCurrent(siteId)
    try {
      await api.post('/shell/show-translator', {
        visible: true,
        siteId,
        fillClipboard: opts?.fillClipboard === true,
      })
      const name = sites.find((s) => s.id === siteId)?.name ?? siteId
      setStatus(
        opts?.fillClipboard
          ? `已打开翻译窗口并尝试填充到 ${name}`
          : `已打开翻译窗口（${name}）`,
      )
    } catch (err) {
      setStatus(err instanceof Error ? err.message : '打开失败')
    }
  }

  async function setDefaultSite(id: string) {
    setCurrent(id)
    await api.put('/translator/site', { id, open: false })
    setStatus(`默认引擎已设为 ${sites.find((s) => s.id === id)?.name ?? id}`)
  }

  return (
    <div className="space-y-4 p-4">
      <div>
        <h1 className="text-lg font-semibold">熔岩翻译</h1>
        <p className="mt-1 text-sm text-gray-500">
          翻译站在独立窗口中运行（独立 WebView2），不嵌在主界面里。快捷键 Alt+Space。
        </p>
      </div>

      <div className="flex flex-wrap gap-2">
        <Button onClick={() => void openWindow()}>打开翻译窗口</Button>
        <Button variant="outline" onClick={() => void openWindow({ fillClipboard: true })}>
          打开并填充剪贴板
        </Button>
        <Button
          variant="outline"
          onClick={async () => {
            await api.post('/shell/show-translator', { visible: false })
            setStatus('已隐藏翻译窗口')
          }}
        >
          隐藏翻译窗口
        </Button>
      </div>

      <div>
        <div className="mb-2 text-sm font-medium text-gray-700">默认翻译引擎</div>
        <div className="flex flex-wrap gap-2">
          {sites.map((site) => (
            <Button
              key={site.id}
              variant={current === site.id ? 'default' : 'outline'}
              className="h-8 text-xs"
              onClick={() => void setDefaultSite(site.id)}
              onDoubleClick={() => void openWindow({ siteId: site.id })}
            >
              {site.name}
            </Button>
          ))}
        </div>
        <p className="mt-1 text-xs text-gray-400">单击设为默认；双击打开该引擎窗口</p>
      </div>

      {status && <p className="text-sm text-gray-600">{status}</p>}
    </div>
  )
}
