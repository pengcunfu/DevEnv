type Props = {
  title: string
  description: string
}

export function PlaceholderPage({ title, description }: Props) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-5 shadow-sm">
      <h1 className="text-lg font-semibold text-gray-900">{title}</h1>
      <p className="mt-2 text-sm text-gray-600">{description}</p>
      <p className="mt-4 text-xs text-gray-400">
        此页正在按功能对等清单迁移中；骨架与 axios / HMR 已接通。
      </p>
    </div>
  )
}
