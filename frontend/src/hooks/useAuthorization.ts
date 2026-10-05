import { useAuth } from './useAuth'

export function useAuthorization() {
  const auth = useAuth()
  return {
    hasRole: auth.hasRole,
    hasPermission: auth.hasPermission,
    hasAllPermissions: auth.hasAllPermissions,
  }
}
