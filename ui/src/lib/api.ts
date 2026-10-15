import axios from 'axios'

const TOKEN_KEY = 'devbox.sessionToken'

export function resolveTokenFromUrl(): string | null {
  const params = new URLSearchParams(window.location.search)
  const token = params.get('token')
  if (token) {
    sessionStorage.setItem(TOKEN_KEY, token)
    params.delete('token')
    const next = `${window.location.pathname}${params.toString() ? `?${params}` : ''}${window.location.hash}`
    window.history.replaceState({}, '', next)
  }
  return sessionStorage.getItem(TOKEN_KEY)
}

resolveTokenFromUrl()

export const api = axios.create({
  baseURL: '/api',
  timeout: 30000,
})

api.interceptors.request.use((config) => {
  const token = sessionStorage.getItem(TOKEN_KEY)
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

export type NavItem = { id: string; title: string }

export type AppConfig = {
  TrayIcon?: boolean
  SelfStarting?: boolean
  BaseEnvPath?: string
  Language?: string
  DarkMode?: boolean
  DoubleClickToLaunch?: boolean
  ShowWindowHotkey?: string
}

export async function fetchHealth() {
  const { data } = await api.get<{ ok: boolean; product: string; version: string }>('/health')
  return data
}

export async function fetchNav() {
  const { data } = await api.get<{ items: NavItem[] }>('/nav')
  return data.items
}

export async function fetchConfig() {
  const { data } = await api.get<AppConfig>('/config')
  return data
}

export async function saveConfig(patch: Partial<AppConfig>) {
  const { data } = await api.put<AppConfig>('/config', patch)
  return data
}
