import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '@/lib/api'
import { Button } from '@/components/ui/button'

type Entry = {
  id: string
  name: string
  command: string
  location: string
  enabled: boolean
}

export function StartupToolPage() {
  const [items, setItems] = useState<Entry[]>([])
  const [name, setName] = useState('')
  const [command, setCommand] = useState('')
  const [message, setMessage] = useState('')

  const load = useCallback(async () => {
    const { data } = await api.get<Entry[]>('/startup')
    setItems(data)
  }, [])

  useEffect(() => {
    void load().catch((err: unknown) => setMessage(err instanceof Error ? err.message : '加载失败'))
  }, [load])

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-3">
        <Link to="/tools" className="text-sm text-blue-600 hover:underline">← 系统工具</Link>
        <h1 className="text-lg font-semibold">启动项管理</h1>
      </div>
      <p className="text-sm text-gray-500">当前用户 Run 注册表与启动文件夹（服务/计划任务仍可在旧壳完整管理）</p>
      <div className="flex flex-wrap gap-2">
        <input className="w-40 rounded-md border bg-white px-2 py-1.5 text-sm" placeholder="名称" value={name} onChange={(e) => setName(e.target.value)} />
        <input className="min-w-[240px] flex-1 rounded-md border bg-white px-2 py-1.5 text-sm" placeholder="命令 / 路径" value={command} onChange={(e) => setCommand(e.target.value)} />
        <Button
          onClick={async () => {
            try {
              await api.post('/startup', { name, command })
              setName('')
              setCommand('')
              setMessage('已添加')
              await load()
            } catch (err) {
              const data = (err as { response?: { data?: { error?: string } } })?.response?.data
              setMessage(data?.error || (err instanceof Error ? err.message : '添加失败'))
            }
          }}
        >
          添加注册表项
        </Button>
        <Button variant="outline" onClick={() => void load()}>刷新</Button>
      </div>
      {message && <div className="text-sm text-gray-600">{message}</div>}
      <div className="overflow-hidden rounded-lg border bg-white">
        <table className="w-full text-left text-sm">
          <thead className="bg-gray-50 text-gray-600">
            <tr>
              <th className="px-3 py-2">名称</th>
              <th className="px-3 py-2">位置</th>
              <th className="px-3 py-2">命令</th>
              <th className="px-3 py-2 w-24">操作</th>
            </tr>
          </thead>
          <tbody>
            {items.map((e) => (
              <tr key={e.id} className="border-t">
                <td className="px-3 py-2">{e.name}</td>
                <td className="px-3 py-2 text-xs text-gray-500">{e.location}</td>
                <td className="max-w-[280px] truncate px-3 py-2 text-xs">{e.command}</td>
                <td className="px-3 py-2">
                  <Button
                    variant="outline"
                    className="h-7 text-xs"
                    onClick={async () => {
                      await api.delete(`/startup/${encodeURIComponent(e.id)}`)
                      await load()
                    }}
                  >
                    删除
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
