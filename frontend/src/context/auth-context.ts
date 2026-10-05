import { createContext } from 'react'
import type { AuthIdentity } from '../types/api'

export type AuthStatus = 'loading' | 'anonymous' | 'authenticated' | 'password-change'

export interface AuthContextValue {
  status: AuthStatus
  identity: AuthIdentity | null
  temporaryUserName: string | null
  login: (userName: string, password: string) => Promise<'authenticated' | 'password-change'>
  logout: () => Promise<void>
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>
  hasRole: (role: string) => boolean
  hasPermission: (permission: string) => boolean
  hasAllPermissions: (permissions: string[]) => boolean
}

export const AuthContext = createContext<AuthContextValue | null>(null)
