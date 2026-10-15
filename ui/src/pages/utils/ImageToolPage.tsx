import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '@/lib/api'
import { Button } from '@/components/ui/button'

type Result = { input: string; output?: string; success: boolean; error?: string }

const FORMATS = ['png', 'jpg', 'webp', 'bmp', 'gif', 'tiff', 'ico']

export function ImageToolPage() {
  const [files, setFiles] = useState<string[]>([])
  const [format, setFormat] = useState('png')
  const [quality, setQuality] = useState(95)
  const [outputDir, setOutputDir] = useState('')
  const [results, setResults] = useState<Result[]>([])
  const [message, setMessage] = useState('')

  async function pickFiles() {
    const { data } = await api.post<{ cancelled: boolean; files: string[] }>('/shell/pick-files', {
      filter: '图片|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.tif;*.webp;*.ico|所有文件|*.*',
    })
    if (!data.cancelled)
      setFiles(data.files ?? [])
  }

  async function pickOutDir() {
    const { data } = await api.post<{ cancelled: boolean; path?: string }>('/shell/pick-folder')
    if (!data.cancelled && data.path)
      setOutputDir(data.path)
  }

  async function convert() {
    try {
      const { data } = await api.post<{ results: Result[] }>('/utils/image/convert', {
        files,
        format,
        quality,
        outputDirectory: outputDir || null,
      })
      setResults(data.results)
      const ok = data.results.filter((r) => r.success).length
      setMessage(`完成：${ok}/${data.results.length}`)
    } catch (err) {
      const data = (err as { response?: { data?: { error?: string } } })?.response?.data
      setMessage(data?.error || (err instanceof Error ? err.message : '转换失败'))
    }
  }

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-3">
        <Link to="/tools" className="text-sm text-blue-600 hover:underline">← 系统工具</Link>
        <h1 className="text-lg font-semibold">图像格式转换器</h1>
      </div>
      <div className="flex flex-wrap gap-2">
        <Button variant="outline" onClick={() => void pickFiles()}>选择图片…</Button>
        <select className="rounded-md border bg-white px-2 text-sm" value={format} onChange={(e) => setFormat(e.target.value)}>
          {FORMATS.map((f) => <option key={f} value={f}>{f.toUpperCase()}</option>)}
        </select>
        <label className="flex items-center gap-1 text-sm">
          质量
          <input type="number" min={1} max={100} className="w-16 rounded-md border px-1 py-1" value={quality} onChange={(e) => setQuality(Number(e.target.value))} />
        </label>
        <input className="min-w-[200px] flex-1 rounded-md border bg-white px-2 py-1.5 text-sm" placeholder="输出目录（空=同目录）" value={outputDir} onChange={(e) => setOutputDir(e.target.value)} />
        <Button variant="outline" onClick={() => void pickOutDir()}>目录…</Button>
        <Button disabled={files.length === 0} onClick={() => void convert()}>转换</Button>
      </div>
      <div className="rounded-lg border bg-white p-3 text-sm">
        <div className="mb-2 text-gray-500">已选 {files.length} 个文件</div>
        <ul className="max-h-40 overflow-auto text-xs text-gray-700">
          {files.map((f) => <li key={f} className="truncate">{f}</li>)}
        </ul>
      </div>
      {message && <div className="text-sm text-gray-600">{message}</div>}
      {results.length > 0 && (
        <div className="overflow-hidden rounded-lg border bg-white">
          <table className="w-full text-left text-sm">
            <thead className="bg-gray-50 text-gray-600">
              <tr>
                <th className="px-3 py-2">输入</th>
                <th className="px-3 py-2">输出</th>
                <th className="px-3 py-2">结果</th>
              </tr>
            </thead>
            <tbody>
              {results.map((r) => (
                <tr key={r.input} className="border-t">
                  <td className="max-w-[200px] truncate px-3 py-2 text-xs">{r.input}</td>
                  <td className="max-w-[200px] truncate px-3 py-2 text-xs">{r.output || '—'}</td>
                  <td className="px-3 py-2 text-xs">{r.success ? '成功' : r.error}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
