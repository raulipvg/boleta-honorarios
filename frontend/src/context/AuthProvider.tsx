import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { authService } from '../services/authService'
import { setAccessToken } from '../services/apiClient'
import type { AuthIdentity } from '../types/api'
import { AuthContext, type AuthContextValue, type AuthStatus } from './auth-context'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('loading')
  const [identity, setIdentity] = useState<AuthIdentity | null>(null)
  const [temporaryUserName, setTemporaryUserName] = useState<string | null>(null)

  const resetSession = useCallback(() => {
    setAccessToken(null)
    setIdentity(null)
    setTemporaryUserName(null)
    setStatus('anonymous')
  }, [])

  useEffect(() => {
    let mounted = true
    const restore = async () => {
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
    const result = await authService.login(userName, password)
    if (result.token.requiresPasswordChange) {
      setTemporaryUserName(userName)
      setIdentity(null)
      setStatus('password-change')
      return 'password-change'
    }
    if (!result.identity) throw new Error('La identidad de usuario no pudo cargarse.')
    setTemporaryUserName(null)
    setIdentity(result.identity)
    setStatus('authenticated')
    return 'authenticated'
  }, [])

  const logout = useCallback(async () => {
    try {
      await authService.logout()
    } finally {
      resetSession()
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
