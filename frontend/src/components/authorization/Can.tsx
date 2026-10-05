import type { ReactNode } from 'react'
import { useAuthorization } from '../../hooks/useAuthorization'

export function Can({ permission, children, fallback = null }: {
  permission: string
  children: ReactNode
  fallback?: ReactNode
}) {
  const { hasPermission } = useAuthorization()
  return hasPermission(permission) ? children : fallback
}
