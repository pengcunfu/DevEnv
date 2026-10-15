import { useEffect, useRef } from 'react'
import { cn } from '@/lib/utils'

export type ContextMenuItem =
  | { type: 'item'; label: string; danger?: boolean; disabled?: boolean; onClick: () => void }
  | { type: 'separator' }

type Props = {
  x: number
  y: number
  items: ContextMenuItem[]
  onClose: () => void
}

/** 简易右键菜单，样式贴近 Avalonia 卡片菜单（运行 / 编辑 / 移除…）。 */
export function ContextMenu({ x, y, items, onClose }: Props) {
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const onDown = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node))
        onClose()
    }
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape')
        onClose()
    }
    window.addEventListener('mousedown', onDown)
    window.addEventListener('keydown', onKey)
    return () => {
      window.removeEventListener('mousedown', onDown)
      window.removeEventListener('keydown', onKey)
    }
  }, [onClose])

  // 避免贴边溢出
  const left = Math.min(x, window.innerWidth - 180)
  const top = Math.min(y, window.innerHeight - items.length * 32 - 16)

  return (
    <div
      ref={ref}
      className="fixed z-[1000] min-w-[148px] rounded-md border border-gray-200 bg-white py-1 shadow-lg"
      style={{ left, top }}
      role="menu"
    >
      {items.map((item, i) => {
        if (item.type === 'separator')
          return <div key={`s-${i}`} className="my-1 border-t border-gray-100" />
        return (
          <button
            key={`${item.label}-${i}`}
            type="button"
            role="menuitem"
            disabled={item.disabled}
            className={cn(
              'block w-full px-3 py-1.5 text-left text-[13px] hover:bg-blue-50 disabled:opacity-40',
              item.danger && 'text-red-600 hover:bg-red-50',
            )}
            onClick={() => {
              item.onClick()
              onClose()
            }}
          >
            {item.label}
          </button>
        )
      })}
    </div>
  )
}
