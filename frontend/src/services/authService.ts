import { apiClient, getCsrfToken, setAccessToken } from './apiClient'
import type { AuthIdentity, AuthToken } from '../types/api'

let restoringSession: Promise<{ token: AuthToken; identity: AuthIdentity }>|null = null

export const authService = {
  restore(): Promise<{ token: AuthToken; identity: AuthIdentity }> {
    if (!restoringSession) {
      restoringSession = restoreSessionOnce().finally(() => { restoringSession = null })
    }
    return restoringSession
  },

  async login(userName: string, password: string): Promise<{ token: AuthToken; identity: AuthIdentity | null }> {
    const csrfToken = await getCsrfToken()
    const { data: token } = await apiClient.post<AuthToken>('/auth/login', { userName, password }, {
      headers: { 'X-CSRF-TOKEN': csrfToken },
      _skipAuthRefresh: true,
    })
    setAccessToken(token.accessToken)
    if (token.requiresPasswordChange) return { token, identity: null }
    const { data: identity } = await apiClient.get<AuthIdentity>('/auth/me')
    return { token, identity }
  },

  async logout(): Promise<void> {
    try {
      const csrfToken = await getCsrfToken()
      await apiClient.post('/auth/logout', {}, { headers: { 'X-CSRF-TOKEN': csrfToken }, _skipAuthRefresh: true })
    } finally {
      setAccessToken(null)
    }
  },

  async changePassword(currentPassword: string, newPassword: string): Promise<void> {
    await apiClient.post('/auth/password/change', { currentPassword, newPassword }, { _skipAuthRefresh: true })
    setAccessToken(null)
  },
}

async function restoreSessionOnce(): Promise<{ token: AuthToken; identity: AuthIdentity }> {
  const csrfToken = await getCsrfToken()
  const { data: token } = await apiClient.post<AuthToken>('/auth/refresh', {}, {
    headers: { 'X-CSRF-TOKEN': csrfToken },
    _skipAuthRefresh: true,
  })
  setAccessToken(token.accessToken)
  if (token.requiresPasswordChange)
    return { token, identity: { userId: '', userName: '', roleCodes: [], permissionCodes: [], requiresPasswordChange: true } }
  const { data: identity } = await apiClient.get<AuthIdentity>('/auth/me')
  return { token, identity }
}
