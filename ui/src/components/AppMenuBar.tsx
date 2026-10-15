import { useEffect, useRef, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, fetchHealth } from '@/lib/api'
import { cn } from '@/lib/utils'

type OpenMenu = 'file' | 'tools' | 'translator' | 'clipboard' | 'settings' | 'help' | null

/**
 * WebView 内绘制的类 Windows 菜单栏（非原生 MenuStrip）。
 */
export function AppMenuBar() {
  const navigate = useNavigate()
  const [open, setOpen] = useState<OpenMenu>(null)
  const [aboutOpen, setAboutOpen] = useState(false)
  const [version, setVersion] = useState('…')
  const rootRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    fetchHealth().then((h) => setVersion(h.version)).catch(() => undefined)
  }, [])

  useEffect(() => {
    if (!open) return
    const onDown = (e: MouseEvent) => {
      if (!rootRef.current?.contains(e.target as Node))
        setOpen(null)
    }
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(null)
    }
    document.addEventListener('mousedown', onDown)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('mousedown', onDown)
      document.removeEventListener('keydown', onKey)
    }
  }, [open])

  function toggle(menu: OpenMenu) {
    setOpen((cur) => (cur === menu ? null : menu))
  }

  function hoverOpen(menu: OpenMenu) {
    if (open) setOpen(menu)
  }

  return (
    <>
      <div
        ref={rootRef}
        className="relative z-50 flex h-6 shrink-0 items-stretch border-b border-[#d4d4d4] bg-[#f0f0f0] text-[12px] text-[#1a1a1a] select-none"
        style={{ fontFamily: 'Segoe UI, Microsoft YaHei UI, sans-serif' }}
      >
        <MenuTop
          label="文件(F)"
          active={open === 'file'}
          onClick={() => toggle('file')}
          onMouseEnter={() => hoverOpen('file')}
        >
          {open === 'file' && (
            <Dropdown>
              <MenuItem
                label="退出(X)"
                shortcut="Alt+F4"
                onClick={() => {
                  setOpen(null)
                  void api.post('/shell/exit')
                }}
              />
            </Dropdown>
          )}
        </MenuTop>

        <MenuTop
          label="工具(T)"
          active={open === 'tools'}
          onClick={() => toggle('tools')}
          onMouseEnter={() => hoverOpen('tools')}
        >
          {open === 'tools' && (
            <Dropdown>
              <MenuItem
                label="JSON 格式化器"
                onClick={() => {
                  setOpen(null)
                  navigate('/tools/json')
                }}
              />
              <MenuItem
                label="Hosts 文件编辑器"
                onClick={() => {
                  setOpen(null)
                  navigate('/tools/hosts')
                }}
              />
              <MenuItem
                label="图像格式转换器"
                onClick={() => {
                  setOpen(null)
                  navigate('/tools/image')
                }}
              />
              <MenuItem
                label="文件哈希计算器"
                onClick={() => {
                  setOpen(null)
                  navigate('/tools/hash')
                }}
              />
              <MenuSeparator />
              <MenuItem
                label="启动项管理"
                onClick={() => {
                  setOpen(null)
                  navigate('/tools/startup')
                }}
              />
            </Dropdown>
          )}
        </MenuTop>

        <MenuTop
          label="熔岩翻译(L)"
          active={open === 'translator'}
          onClick={() => {
            setOpen(null)
            navigate('/translator')
            void api.post('/shell/show-translator', { visible: true })
          }}
          onMouseEnter={() => hoverOpen('translator')}
        />

        <MenuTop
          label="熔岩剪贴板(C)"
          active={open === 'clipboard'}
          onClick={() => {
            setOpen(null)
            navigate('/clipboard')
            void api.post('/shell/show-clipboard', { visible: true })
          }}
          onMouseEnter={() => hoverOpen('clipboard')}
        />

        <MenuTop
          label="设置(S)"
          active={open === 'settings'}
          onClick={() => {
            setOpen(null)
            navigate('/settings')
          }}
          onMouseEnter={() => hoverOpen('settings')}
        />

        <MenuTop
          label="帮助(H)"
          active={open === 'help'}
          onClick={() => toggle('help')}
          onMouseEnter={() => hoverOpen('help')}
        >
          {open === 'help' && (
            <Dropdown>
              <MenuItem
                label="关于程序盒子(A)..."
                onClick={() => {
                  setOpen(null)
                  setAboutOpen(true)
                }}
              />
            </Dropdown>
          )}
        </MenuTop>
      </div>

      {aboutOpen && (
        <AboutDialog version={version} onClose={() => setAboutOpen(false)} />
      )}
    </>
  )
}

function MenuTop({
  label,
  active,
  onClick,
  onMouseEnter,
  children,
}: {
  label: string
  active: boolean
  onClick: () => void
  onMouseEnter: () => void
  children?: ReactNode
}) {
  return (
    <div className="relative">
      <button
        type="button"
        className={cn(
          'h-full px-2.5 outline-none',
          active ? 'bg-[#cce8ff] text-black' : 'hover:bg-[#e5e5e5]',
        )}
        onClick={onClick}
        onMouseEnter={onMouseEnter}
      >
        {label}
      </button>
      {children}
    </div>
  )
}

function Dropdown({ children }: { children: ReactNode }) {
  return (
    <div className="absolute left-0 top-full min-w-[200px] border border-[#a0a0a0] bg-white py-0.5 shadow-[2px_2px_6px_rgba(0,0,0,.18)]">
      {children}
    </div>
  )
}

function MenuItem({
  label,
  shortcut,
  onClick,
}: {
  label: string
  shortcut?: string
  onClick: () => void
}) {
  return (
    <button
      type="button"
      className="flex w-full items-center justify-between gap-8 px-3 py-[3px] text-left hover:bg-[#0078d4] hover:text-white"
      onClick={onClick}
    >
      <span>{label}</span>
      {shortcut && <span className="text-[11px] opacity-80">{shortcut}</span>}
    </button>
  )
}

function MenuSeparator() {
  return <div className="my-0.5 border-t border-[#e0e0e0]" />
}

function AboutDialog({ version, onClose }: { version: string; onClose: () => void }) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [onClose])

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/25" onClick={onClose}>
      <div
        className="w-[420px] rounded-sm border border-[#7a7a7a] bg-[#f0f0f0] shadow-lg"
        style={{ fontFamily: 'Segoe UI, Microsoft YaHei UI, sans-serif' }}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between border-b border-[#d0d0d0] bg-white px-3 py-1.5 text-[12px]">
          <span>关于程序盒子</span>
          <button type="button" className="px-1.5 hover:bg-red-600 hover:text-white" onClick={onClose}>
            ✕
          </button>
        </div>
        <div className="space-y-2 bg-white px-5 py-4 text-[13px] text-gray-800">
          <div className="text-base font-semibold">程序盒子</div>
          <div className="text-gray-500">版本 {version}</div>
          <p className="leading-relaxed text-gray-700">
            专为程序员设计的多语言开发环境管理工具，支持 Java、Node.js、Python、C++ 等多种开发环境。
          </p>
          <div className="border-t border-gray-200 pt-2 text-[12px] text-gray-500 space-y-0.5">
            <div>熔岩翻译 · Alt+Space</div>
            <div>熔岩剪贴板 · Ctrl+Space</div>
            <div>显示窗口 · 可在设置中配置</div>
          </div>
          <div className="pt-1 text-[12px] text-gray-400">© FNSoftware</div>
        </div>
        <div className="flex justify-end border-t border-[#d0d0d0] bg-[#f0f0f0] px-3 py-2">
          <button
            type="button"
            className="min-w-[72px] rounded-sm border border-[#adadad] bg-[#e1e1e1] px-3 py-1 text-[12px] hover:bg-[#e5f1fb] hover:border-[#0078d4]"
            onClick={onClose}
          >
            确定
          </button>
        </div>
      </div>
    </div>
  )
}
