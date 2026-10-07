import { apiClient } from '../apiClient'

export const profileService = {
  async get() {
    return (await apiClient.get<{ id: string; userId: string; name: string; rut: string | null }>('/profile')).data
  },
  async update(name: string, rut: string | null) {
    return (await apiClient.put('/profile', { name, rut })).data
  },
}
