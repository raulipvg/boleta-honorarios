const pendingLogoutKey = 'gestion-honorarios:logout-pending'

export function hasPendingLogout(): boolean {
  try {
    return window.sessionStorage.getItem(pendingLogoutKey) === 'true'
  } catch {
    return false
  }
}

export function markLogoutPending(): void {
  try {
    window.sessionStorage.setItem(pendingLogoutKey, 'true')
  } catch {
    // The in-memory session is still cleared when browser storage is unavailable.
  }
}

export function clearLogoutPending(): void {
  try {
    window.sessionStorage.removeItem(pendingLogoutKey)
  } catch {
    // No action is needed when browser storage is unavailable.
  }
}
