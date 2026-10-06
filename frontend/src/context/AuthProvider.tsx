import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { authService } from '../services/authService'
import { setAccessToken } from '../services/apiClient'
import { clearLogoutPending, hasPendingLogout, markLogoutPending } from '../services/logoutState'
import type { AuthIdentity } from '../types/api'
import { AuthContext, type AuthContextValue, type AuthStatus } from './auth-context'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('loading')
  const [identity, setIdentity] = useState<AuthIdentity | null>(null)
  const [temporaryUserName, setTemporaryUserName] = useState<string | null>(null)
  const logoutRequest = useRef<Promise<void> | null>(null)

  const resetSession = useCallback(() => {
    setAccessToken(null)
    setIdentity(null)
    setTemporaryUserName(null)
    setStatus('anonymous')
  }, [])

  useEffect(() => {
    let mounted = true
    const restore = async () => {
      if (hasPendingLogout()) {
        const request = authService.logout()
        logoutRequest.current = request
        try {
          await request
          clearLogoutPending()
        } catch {
          // Keep the marker so a refresh cannot restore the session before logout is retried.
        } finally {
          if (logoutRequest.current === request) logoutRequest.current = null
          if (mounted) resetSession()
        }
        return
      }

      try {
        const result = await authService.restore()
        if (!mounted) return
        if (result.token.requiresPasswordChange) {
          setStatus('password-change')
          return
        }
        setIdentity(result.identity)
        setStatus('authenticated')
      } catch {
        if (mounted) resetSession()
      }
    }
    const onExpired = () => resetSession()
    window.addEventListener('auth:session-expired', onExpired)
    void restore()
    return () => {
      mounted = false
      window.removeEventListener('auth:session-expired', onExpired)
    }
  }, [resetSession])

  const login = useCallback(async (userName: string, password: string): Promise<'authenticated' | 'password-change'> => {
    if (logoutRequest.current) {
      try { await logoutRequest.current } catch { /* A fresh login may replace the old cookie. */ }
    }
    if (hasPendingLogout()) {
      try {
        await authService.logout()
        clearLogoutPending()
      } catch { /* The successful login below will replace the stale refresh cookie. */ }
    }

    const result = await authService.login(userName, password)
    if (result.token.requiresPasswordChange) {
      setTemporaryUserName(userName)
      setIdentity(null)
      setStatus('password-change')
      return 'password-change'
    }
    if (!result.identity) throw new Error('La identidad de usuario no pudo cargarse.')
    clearLogoutPending()
    setTemporaryUserName(null)
    setIdentity(result.identity)
    setStatus('authenticated')
    return 'authenticated'
  }, [])

  const logout = useCallback(async () => {
    markLogoutPending()
    resetSession()
    const request = authService.logout()
    logoutRequest.current = request
    try {
      await request
      clearLogoutPending()
    } catch {
      // Stay logged out locally and retry server-side cookie cleanup before session restoration.
    } finally {
      if (logoutRequest.current === request) logoutRequest.current = null
    }
  }, [resetSession])

  const changePassword = useCallback(async (currentPassword: string, newPassword: string) => {
    await authService.changePassword(currentPassword, newPassword)
    resetSession()
  }, [resetSession])

  const value = useMemo<AuthContextValue>(() => ({
    status,
    identity,
    temporaryUserName,
    login,
    logout,
    changePassword,
    hasRole: role => identity?.roleCodes.includes(role) ?? false,
    hasPermission: permission => identity?.permissionCodes.includes(permission) ?? false,
    hasAllPermissions: permissions => permissions.every(permission => identity?.permissionCodes.includes(permission) ?? false),
  }), [status, identity, temporaryUserName, login, logout, changePassword])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
