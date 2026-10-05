import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { Spin } from 'antd'
import { useAuth } from '../hooks/useAuth'

interface ProtectedRouteProps {
  requiredPermissions?: string[]
  requiredRoles?: string[]
  roleMode?: 'any' | 'all'
}

export function ProtectedRoute({ requiredPermissions, requiredRoles, roleMode = 'any' }: ProtectedRouteProps) {
  const auth = useAuth()
  const location = useLocation()

  if (auth.status === 'loading') return <div className="page-loading"><Spin size="large" /></div>
  if (auth.status === 'anonymous') return <Navigate to="/login" replace state={{ from: location.pathname }} />
  if (auth.status === 'password-change') return <Navigate to="/password-change" replace />

  if (requiredPermissions && !auth.hasAllPermissions(requiredPermissions)) return <Navigate to="/forbidden" replace />
  if (requiredRoles) {
    const allowed = roleMode === 'all'
      ? requiredRoles.every(auth.hasRole)
      : requiredRoles.some(auth.hasRole)
    if (!allowed) return <Navigate to="/forbidden" replace />
  }
  return <Outlet />
}
