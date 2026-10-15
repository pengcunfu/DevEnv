import { NavLink, Outlet } from 'react-router-dom'
import { useEffect, useState } from 'react'
import { fetchHealth, fetchNav, type NavItem } from '@/lib/api'
import { AppMenuBar } from '@/components/AppMenuBar'
import { cn } from '@/lib/utils'

const fallbackNav: NavItem[] = [
  { id: 'apps', title: '本地应用' },
  { id: 'folders', title: '文件夹' },
  { id: 'webs', title: '网站' },
  { id: 'devenv', title: '开发环境' },
  { id: 'lava', title: '熔岩环境' },
  { id: 'network', title: '网络监控' },
  { id: 'processes', title: '进程监控' },
  { id: 'tools', title: '系统工具' },
]

export function Layout() {
  const [nav, setNav] = useState<NavItem[]>(fallbackNav)
  const [version, setVersion] = useState('…')
  const [status, setStatus] = useState('连接中')

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      try {
        const [items, health] = await Promise.all([fetchNav(), fetchHealth()])
        if (cancelled) return
        setNav(items)
        setVersion(health.version)
        setStatus('就绪')
      } catch {
        if (!cancelled) setStatus('API 未连接（请先启动 DevBox.Host）')
      }
    })()
    return () => {
      cancelled = true
    }
  }, [])

  return (
    <div className="flex h-full flex-col">
      <AppMenuBar />
      <div className="flex min-h-0 flex-1">
        <aside className="flex w-40 shrink-0 flex-col border-r border-gray-200 bg-white">
          <nav className="flex-1 overflow-y-auto p-2">
            {nav.map((item) => (
              <NavLink
                key={item.id}
                to={`/${item.id}`}
                className={({ isActive }) =>
                  cn(
                    'mb-0.5 block rounded-md px-2.5 py-1.5 text-[13px] text-gray-700 hover:bg-gray-50',
                    isActive && 'bg-blue-50 font-medium text-blue-700',
                  )
                }
              >
                {item.title}
              </NavLink>
            ))}
          </nav>
        </aside>
        <main className="flex min-h-0 min-w-0 flex-1 flex-col overflow-auto bg-[#f3f4f6]">
          <Outlet />
        </main>
      </div>
      <footer className="flex h-6 items-center justify-between border-t border-gray-200 bg-white px-3 text-[11px] text-gray-500">
        <span>{status}</span>
        <span>版本 {version}</span>
      </footer>
    </div>
  )
}
