import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '@/lib/api'
import { Button } from '@/components/ui/button'

export function HostsToolPage() {
  const [text, setText] = useState('')
  const [path, setPath] = useState('')
  const [canWrite, setCanWrite] = useState(false)
  const [message, setMessage] = useState('')

  const load = useCallback(async () => {
    const { data } = await api.get<{ path: string; text: string; canWrite: boolean }>('/utils/hosts')
    setPath(data.path)
    setText(data.text)
    setCanWrite(data.canWrite)
  }, [])

  useEffect(() => {
    void load().catch((err: unknown) => setMessage(err instanceof Error ? err.message : '加载失败'))
  }, [load])

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-3">
        <Link to="/tools" className="text-sm text-blue-600 hover:underline">← 系统工具</Link>
        <h1 className="text-lg font-semibold">Hosts 编辑器</h1>
      </div>
      <p className="text-xs text-gray-500">{path}{canWrite ? ' · 可写' : ' · 只读（需管理员）'}</p>
      <textarea
        className="h-[420px] w-full rounded-lg border bg-white p-3 font-mono text-sm"
        value={text}
        onChange={(e) => setText(e.target.value)}
      />
      <div className="flex flex-wrap gap-2">
        <Button
          onClick={async () => {
            try {
              await api.put('/utils/hosts', { text })
              setMessage('已保存')
              setCanWrite(true)
            } catch (err) {
              const data = (err as { response?: { data?: { error?: string; needElevation?: boolean } } })?.response?.data
              setMessage(data?.error || (err instanceof Error ? err.message : '保存失败'))
            }
          }}
        >
          保存
        </Button>
        <Button
          variant="outline"
          onClick={async () => {
            try {
              const { data } = await api.post<{ message?: string }>('/utils/hosts/elevate')
              setMessage(data.message || '已请求提权')
            } catch (err) {
              const data = (err as { response?: { data?: { error?: string } } })?.response?.data
              setMessage(data?.error || (err instanceof Error ? err.message : '提权失败'))
            }
          }}
        >
          以管理员打开
        </Button>
        <Button variant="outline" onClick={() => void load()}>重新加载</Button>
        {message && <span className="self-center text-sm text-gray-500">{message}</span>}
      </div>
    </div>
  )
}
