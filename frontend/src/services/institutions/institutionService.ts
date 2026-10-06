import { apiClient } from '../apiClient'
import type { Institution } from '../../types/api'

export const institutionService = {
  async list() {
    return (await apiClient.get<Institution[]>('/institutions')).data
  },
  async create(name: string) {
    return (await apiClient.post<Institution>('/institutions', { name })).data
  },
  async update(id: string, name: string, active: boolean) {
    return (await apiClient.put<Institution>(`/institutions/${id}`, { name, active })).data
  },
}
