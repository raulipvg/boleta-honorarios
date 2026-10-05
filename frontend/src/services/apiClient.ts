import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios'

declare module 'axios' {
  interface AxiosRequestConfig {
    _authRetried?: boolean
    _skipAuthRefresh?: boolean
  }
  interface InternalAxiosRequestConfig {
    _authRetried?: boolean
    _skipAuthRefresh?: boolean
  }
}

const configuredApiUrl = import.meta.env.VITE_API_URL?.trim().replace(/\/+$/, '')
const apiBaseUrl = configuredApiUrl
  ? configuredApiUrl.endsWith('/api') ? configuredApiUrl : `${configuredApiUrl}/api`
  : '/api'

export const apiClient = axios.create({
  baseURL: apiBaseUrl,
  timeout: 15_000,
  withCredentials: true,
  headers: { 'Content-Type': 'application/json' },
})

const rawClient = axios.create({ baseURL: apiBaseUrl, timeout: 15_000, withCredentials: true })
let accessToken: string | null = null
let csrfToken: string | null = null
let csrfRequest: Promise<string> | null = null
let refreshRequest: Promise<string> | null = null

export function setAccessToken(token: string | null): void {
  accessToken = token
}

export function clearCsrfToken(): void {
  csrfToken = null
}

export async function getCsrfToken(): Promise<string> {
  if (csrfToken) return csrfToken
  if (!csrfRequest) {
    csrfRequest = rawClient.get<{ requestToken: string }>('/auth/csrf', { _skipAuthRefresh: true })
      .then(({ data }) => {
        csrfToken = data.requestToken
        return csrfToken
      })
      .finally(() => { csrfRequest = null })
  }
  return csrfRequest
}

function isAuthEndpoint(url: string | undefined): boolean {
  return /\/auth\/(csrf|login|refresh|logout|logout-all|password\/change)/.test(url ?? '')
}

async function refreshAccessToken(): Promise<string> {
  if (!refreshRequest) {
    refreshRequest = getCsrfToken()
      .then(token => rawClient.post<{ accessToken: string; requiresPasswordChange: boolean }>(
        '/auth/refresh', {}, { headers: { 'X-CSRF-TOKEN': token }, _skipAuthRefresh: true },
      ))
      .then(({ data }) => {
        if (data.requiresPasswordChange) throw new Error('Se requiere cambiar la contraseña.')
        accessToken = data.accessToken
        return data.accessToken
      })
      .finally(() => { refreshRequest = null })
  }
  return refreshRequest
}

apiClient.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  if (accessToken) config.headers.Authorization = `Bearer ${accessToken}`
  return config
})

apiClient.interceptors.response.use(response => response, async (error: AxiosError) => {
  const original = error.config as InternalAxiosRequestConfig | undefined
  if (error.response?.status !== 401 || !original || original._authRetried || original._skipAuthRefresh || isAuthEndpoint(original.url)) {
    return Promise.reject(error)
  }

  original._authRetried = true
  try {
    const token = await refreshAccessToken()
    original.headers.Authorization = `Bearer ${token}`
    return await apiClient(original)
  } catch {
    accessToken = null
    window.dispatchEvent(new Event('auth:session-expired'))
    return Promise.reject(error)
  }
})

export function getApiErrorMessage(error: unknown, fallback = 'No fue posible completar la operación.'): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as { detail?: string; message?: string } | undefined
    return data?.detail ?? data?.message ?? fallback
  }
  return error instanceof Error ? error.message : fallback
}
