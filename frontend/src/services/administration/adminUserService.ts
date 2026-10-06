import { apiClient } from '../apiClient'
import type { AccountSummary } from '../../types/api'

export const adminUserService = {
  async list() {
    return (await apiClient.get<AccountSummary[]>('/admin/users')).data
  },
  async create(input: { userName: string; temporaryPassword: string; roleCodes: string[]; professionalName?: string }) {
    return (await apiClient.post<AccountSummary>('/admin/users', input)).data
  },
  async resetPassword(id: string, temporaryPassword: string) {
    await apiClient.post(`/admin/users/${id}/reset-password`, { temporaryPassword })
  },
}
