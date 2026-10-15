const TOKEN_KEY = 'devbox.sessionToken'

/** 将本地图标/图片路径转为经 API 鉴权代理的 URL（img 无法带 Authorization，走 query token） */
export function mediaUrl(path?: string | null): string | undefined {
  if (!path) return undefined
  let url = path.startsWith('/api/')
    ? path
    : `/api/media/file?path=${encodeURIComponent(path)}`
  const token = sessionStorage.getItem(TOKEN_KEY)
  if (token && !url.includes('token=')) {
    url += (url.includes('?') ? '&' : '?') + `token=${encodeURIComponent(token)}`
  }
  return url
}
