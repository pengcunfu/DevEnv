import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '@/lib/api'
import { Button } from '@/components/ui/button'

const ALGOS = ['MD5', 'SHA1', 'SHA256', 'SHA384', 'SHA512'] as const

export function HashToolPage() {
  const [mode, setMode] = useState<'file' | 'text'>('file')
  const [path, setPath] = useState('')
  const [text, setText] = useState('')
  const [algo, setAlgo] = useState<string>('SHA256')
  const [result, setResult] = useState('')
  const [message, setMessage] = useState('')

  async function pickFile() {
    const { data } = await api.post<{ cancelled: boolean; files: string[] }>('/shell/pick-files', {
      filter: '所有文件|*.*',
    })
    if (!data.cancelled && data.files?.[0])
      setPath(data.files[0])
  }

  async function calc() {
    try {
      const { data } = await api.post<{ hash: string; algorithm: string; elapsedMs: number }>('/utils/hash', {
        algorithm: algo,
        path: mode === 'file' ? path : undefined,
        text: mode === 'text' ? text : undefined,
      })
      setResult(data.hash)
      setMessage(`${data.algorithm} · ${data.elapsedMs.toFixed(0)} ms`)
    } catch (err) {
      const data = (err as { response?: { data?: { error?: string } } })?.response?.data
      setMessage(data?.error || (err instanceof Error ? err.message : '计算失败'))
      setResult('')
    }
  }

  return (
    <div className="mx-auto max-w-2xl space-y-3">
      <div className="flex items-center gap-3">
        <Link to="/tools" className="text-sm text-blue-600 hover:underline">← 系统工具</Link>
        <h1 className="text-lg font-semibold">文件哈希计算器</h1>
      </div>
      <div className="flex gap-2">
        <Button variant={mode === 'file' ? 'default' : 'outline'} onClick={() => setMode('file')}>文件</Button>
        <Button variant={mode === 'text' ? 'default' : 'outline'} onClick={() => setMode('text')}>文本</Button>
        <select className="rounded-md border bg-white px-2 text-sm" value={algo} onChange={(e) => setAlgo(e.target.value)}>
          {ALGOS.map((a) => <option key={a} value={a}>{a}</option>)}
        </select>
      </div>
      {mode === 'file' ? (
        <div className="flex gap-2">
          <input className="flex-1 rounded-md border bg-white px-2 py-1.5 text-sm" value={path} onChange={(e) => setPath(e.target.value)} placeholder="文件路径" />
          <Button variant="outline" onClick={() => void pickFile()}>浏览…</Button>
        </div>
      ) : (
        <textarea className="h-40 w-full rounded-lg border bg-white p-3 text-sm" value={text} onChange={(e) => setText(e.target.value)} />
      )}
      <Button onClick={() => void calc()}>计算</Button>
      {result && (
        <div className="rounded-lg border bg-white p-3">
          <div className="text-xs text-gray-500 mb-1">{message}</div>
          <code className="break-all text-sm">{result}</code>
        </div>
      )}
      {!result && message && <div className="text-sm text-gray-500">{message}</div>}
    </div>
  )
}
