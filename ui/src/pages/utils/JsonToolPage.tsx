import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '@/lib/api'
import { Button } from '@/components/ui/button'

export function JsonToolPage() {
  const [text, setText] = useState('{\n  "hello": "world"\n}')
  const [message, setMessage] = useState('')

  async function run(minify: boolean) {
    try {
      const { data } = await api.post<{ text: string }>('/utils/json/format', { text, minify })
      setText(data.text)
      setMessage(minify ? '已压缩' : '已格式化')
    } catch (err) {
      const data = (err as { response?: { data?: { error?: string } } })?.response?.data
      setMessage(data?.error || (err instanceof Error ? err.message : '失败'))
    }
  }

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-3">
        <Link to="/tools" className="text-sm text-blue-600 hover:underline">← 系统工具</Link>
        <h1 className="text-lg font-semibold">JSON 格式化器</h1>
      </div>
      <textarea
        className="h-[420px] w-full rounded-lg border bg-white p-3 font-mono text-sm"
        value={text}
        onChange={(e) => setText(e.target.value)}
      />
      <div className="flex gap-2">
        <Button onClick={() => void run(false)}>格式化</Button>
        <Button variant="outline" onClick={() => void run(true)}>压缩</Button>
        {message && <span className="self-center text-sm text-gray-500">{message}</span>}
      </div>
    </div>
  )
}
